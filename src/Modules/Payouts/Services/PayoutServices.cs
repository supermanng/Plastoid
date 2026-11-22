using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payouts.Domain;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Payouts.Services;

public sealed class PayoutOptions
{
    public const string SectionName = "Payouts";
    public int CourseSaleHoldDays { get; set; } = 7;
    public long MinimumPayoutNaira { get; set; } = 5_000;
}

/// <summary>Which liability a payee is paid from: 2010 (sub-ledger per tutor) or 2060 (per organisation, ADR 0019).</summary>
internal static class PayeeAccounts
{
    public static string AccountCode(string payeeType) => payeeType == PayeeTypes.Organisation ? AccountCodes.ProviderPayable : AccountCodes.TutorPayable;

    public static string OwnerType(string payeeType) => payeeType == PayeeTypes.Organisation ? SubLedgerOwnerTypes.Organisation : SubLedgerOwnerTypes.Tutor;
}

/// <summary>Journal entry 4 (and its reversal) of blueprint §18, with the provider variant of ADR 0019.</summary>
internal static class PayoutPostings
{
    /// <summary>Entry 4: payee paid. Dr 2010 (tutor) / Cr 1020; organisation: Dr 2060 (organisation) / Cr 1020.</summary>
    public static JournalEntryDraft Entry4Settled(Payout payout, DateTime occurredAt)
        => new($"payout:{payout.Id:N}:settled", payout.PayeeType == PayeeTypes.Organisation ? "provider_payout" : "tutor_payout", $"Payout {payout.ProviderReference} to {payout.AccountName}",
            [PostingDraft.Dr(PayeeAccounts.AccountCode(payout.PayeeType), payout.Amount, PayeeAccounts.OwnerType(payout.PayeeType), payout.PayeeId), PostingDraft.Cr(AccountCodes.BankClientFunds, payout.Amount)], occurredAt);

    /// <summary>Reversal: the bank returned the money. Dr 1020 / Cr 2010 (tutor) or 2060 (organisation).</summary>
    public static JournalEntryDraft Reversal(Payout payout, DateTime occurredAt)
        => new($"payout:{payout.Id:N}:reversed", payout.PayeeType == PayeeTypes.Organisation ? "provider_payout_reversal" : "tutor_payout_reversal", $"Reversal of payout {payout.ProviderReference}",
            [PostingDraft.Dr(AccountCodes.BankClientFunds, payout.Amount), PostingDraft.Cr(PayeeAccounts.AccountCode(payout.PayeeType), payout.Amount, PayeeAccounts.OwnerType(payout.PayeeType), payout.PayeeId)], occurredAt);
}

public sealed record EarningsSummary(Money Gross, Money Commission, Money Net, Money Held, Money Pending, Money Paid, Money Payable, Money LedgerPayable, bool DisputeHold);

/// <summary>What a payee can be paid now: released earnings less what was paid or is in flight, never more than the ledger's 2010 (tutor) or 2060 (organisation) balance.</summary>
public sealed class PayableCalculator(PayoutsDbContext db, ILedgerService ledger)
{
    public async Task<EarningsSummary> SummariseAsync(string payeeType, Guid payeeId, DateTime now, CancellationToken ct)
    {
        var earnings = await db.Earnings.AsNoTracking().Where(e => e.PayeeType == payeeType && e.PayeeId == payeeId).ToListAsync(ct);
        var payouts = await db.Payouts.AsNoTracking().Where(p => p.PayeeType == payeeType && p.PayeeId == payeeId).ToListAsync(ct);
        var dispute = await db.Holds.AsNoTracking().AnyAsync(h => h.PayeeType == payeeType && h.PayeeId == payeeId && h.ReleasedAt == null && h.ReleasesAt > now, ct);
        var ledgerPayable = await ledger.GetBalanceAsync(PayeeAccounts.AccountCode(payeeType), payeeId, ct);
        return Summarise(earnings, payouts, dispute, ledgerPayable);
    }

    public static EarningsSummary Summarise(IReadOnlyList<Earning> earnings, IReadOnlyList<Payout> payouts, bool disputeHold, Money ledgerPayable)
    {
        var gross = Sum(earnings.Select(e => e.Gross));
        var commission = Sum(earnings.Select(e => e.Commission));
        var net = Sum(earnings.Select(e => e.PayableNet));
        var held = Sum(earnings.Where(e => e.Status == EarningStatus.Held).Select(e => e.PayableNet));
        var available = Sum(earnings.Where(e => e.Status == EarningStatus.Available).Select(e => e.PayableNet));
        var pending = Sum(payouts.Where(p => p.CountsAsPending).Select(p => p.Amount));
        var paid = Sum(payouts.Where(p => p.Status == PayoutStatus.Settled).Select(p => p.Amount));
        var byEarnings = available - paid - pending;
        var byLedger = ledgerPayable - pending;
        var payable = byEarnings < byLedger ? byEarnings : byLedger;
        if (payable.IsNegative)
        {
            payable = Money.Zero();
        }

        return new EarningsSummary(gross, commission, net, held, pending, paid, payable, ledgerPayable, disputeHold);
    }

    private static Money Sum(IEnumerable<Money> amounts) => amounts.Aggregate(Money.Zero(), (acc, m) => acc + m);
}

/// <summary>A payee with a payable balance that the run did not pay. TutorUserId is the payee id for tutors (older clients), null for organisations.</summary>
public sealed record SkippedPayee(string PayeeType, Guid PayeeId, string Reason, Money Payable)
{
    public Guid? TutorUserId => PayeeType == PayeeTypes.Tutor ? PayeeId : null;
}

public sealed record BatchDraft(PayoutBatch Batch, IReadOnlyList<Payout> Payouts, IReadOnlyList<SkippedPayee> Skipped);

/// <summary>A (payee type, payee id) pair with earnings.</summary>
public sealed record PayeeKey(string PayeeType, Guid PayeeId);

/// <summary>Builds a payout batch (CreatePayoutBatch): minimum ₦5,000, name-matched account, no open dispute, payouts not frozen. Tutors and organisations alike (ADR 0019).</summary>
public sealed class PayoutRunner(PayoutsDbContext db, PayableCalculator calculator, PayeeDestinations destinations, IPayoutControls controls, IOptions<PayoutOptions> options, ICurrentTenant tenant)
{
    public async Task<Result<BatchDraft>> CreateBatchAsync(Guid makerId, DateTime now, CancellationToken ct)
    {
        if (await controls.ArePayoutsFrozenAsync(ct))
        {
            return Error.Conflict("payouts_frozen", "Payouts are frozen by the reconciliation control; resolve the shortfall first.");
        }

        var minimum = Money.NairaAmount(options.Value.MinimumPayoutNaira);
        var payees = await db.Earnings.AsNoTracking().Where(e => e.Status == EarningStatus.Available).Select(e => new PayeeKey(e.PayeeType, e.PayeeId)).Distinct().ToListAsync(ct);
        var payouts = new List<Payout>();
        var skipped = new List<SkippedPayee>();
        var batchId = Entity.NewId();
        foreach (var payee in payees.OrderBy(p => p.PayeeType, StringComparer.Ordinal).ThenBy(p => p.PayeeId))
        {
            var summary = await calculator.SummariseAsync(payee.PayeeType, payee.PayeeId, now, ct);
            if (summary.Payable < minimum)
            {
                if (!summary.Payable.IsZero)
                {
                    skipped.Add(new SkippedPayee(payee.PayeeType, payee.PayeeId, "below_minimum", summary.Payable));
                }

                continue;
            }

            if (summary.DisputeHold)
            {
                skipped.Add(new SkippedPayee(payee.PayeeType, payee.PayeeId, "dispute_open", summary.Payable));
                continue;
            }

            var account = await destinations.GetAsync(payee.PayeeType, payee.PayeeId, ct);
            if (account is null)
            {
                skipped.Add(new SkippedPayee(payee.PayeeType, payee.PayeeId, "no_payout_account", summary.Payable));
                continue;
            }

            if (!account.NameMatched)
            {
                skipped.Add(new SkippedPayee(payee.PayeeType, payee.PayeeId, "name_mismatch", summary.Payable));
                continue;
            }

            payouts.Add(Payout.Create(tenant.TenantId, batchId, payee.PayeeType, payee.PayeeId, summary.Payable, account.BankCode, account.AccountNumberMasked, account.AccountName));
        }

        if (payouts.Count == 0)
        {
            return Error.Conflict("nothing_to_pay", skipped.Count == 0 ? "No payee has a payable balance." : "Every payable payee was skipped: " + string.Join(", ", skipped.Select(s => $"{s.PayeeType}:{s.PayeeId:N}={s.Reason}")));
        }

        var sequence = await db.Batches.CountAsync(b => b.CreatedAt.Year == now.Year, ct) + 1;
        var reference = $"PB-{now:yyyy}-W{ISOWeek.GetWeekOfYear(now):D2}-{sequence:D3}";
        var batch = PayoutBatch.Create(tenant.TenantId, reference, makerId, payouts.Count, payouts.Aggregate(Money.Zero(), (acc, p) => acc + p.Amount));
        // Payout rows were created with the batch id chosen above; the aggregate keeps that id.
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(batch, batchId);
        db.Batches.Add(batch);
        db.Payouts.AddRange(payouts);
        return new BatchDraft(batch, payouts, skipped);
    }
}

/// <summary>
/// Applies a provider status to a payout: entry 4 on settlement, reversal entry when the bank returns the money. Tutors are notified in person;
/// an organisation has no single recipient, so only the integration event is raised (its portal shows the statement).
/// </summary>
internal sealed class PayoutSettlement(PayoutsDbContext db, ILedgerService ledger, IOutbox outbox)
{
    /// <summary>TutorUserId on the contract events: the payee id for tutors, Guid.Empty for organisations.</summary>
    private static Guid LegacyTutorId(Payout payout) => payout.TutorUserId ?? Guid.Empty;

    private void Notify(Payout payout, string template, Dictionary<string, string> data)
    {
        if (payout.TutorUserId is { } tutor)
        {
            outbox.Enqueue(NotificationRequested.To(tutor, template, data));
        }
    }

    public async Task<Result<string>> ApplyAsync(Payout payout, string eventType, string? bankReference, string? reason, DateTime now, CancellationToken ct)
    {
        switch (eventType)
        {
            case "transfer.success":
                if (payout.Status == PayoutStatus.Settled)
                {
                    return "already_settled";
                }

                var entry = await ledger.PostAsync(PayoutPostings.Entry4Settled(payout, now), ct);
                if (entry.IsFailure)
                {
                    return Result.Failure<string>(entry.Error);
                }

                var settled = payout.Settle(bankReference ?? string.Empty, entry.Value, now);
                if (settled.IsFailure)
                {
                    return Result.Failure<string>(settled.Error);
                }

                outbox.Enqueue(new Contracts.PayoutSettled(payout.Id, payout.Id, LegacyTutorId(payout), payout.Amount, payout.BankReference ?? string.Empty, payout.PayeeType, payout.PayeeId));
                Notify(payout, "payouts.payout.settled", new Dictionary<string, string> { ["payoutId"] = payout.Id.ToString("D"), ["amount"] = payout.Amount.ToString(), ["bankRef"] = payout.BankReference ?? string.Empty });
                return "settled";
            case "transfer.failed":
                var failed = payout.Fail(reason ?? "Transfer failed", now);
                if (failed.IsFailure)
                {
                    return Result.Failure<string>(failed.Error);
                }

                outbox.Enqueue(new Contracts.PayoutFailed(payout.Id, payout.Id, LegacyTutorId(payout), payout.Amount, payout.FailureReason!, false, payout.PayeeType, payout.PayeeId));
                Notify(payout, "payouts.payout.failed", new Dictionary<string, string> { ["payoutId"] = payout.Id.ToString("D"), ["amount"] = payout.Amount.ToString(), ["reason"] = payout.FailureReason! });
                return "failed";
            case "transfer.reversed":
                if (payout.Status == PayoutStatus.Reversed)
                {
                    return "already_reversed";
                }

                var reversal = await ledger.PostAsync(PayoutPostings.Reversal(payout, now), ct);
                if (reversal.IsFailure)
                {
                    return Result.Failure<string>(reversal.Error);
                }

                var reversed = payout.Reverse(reason ?? "Transfer reversed by the bank", reversal.Value, now);
                if (reversed.IsFailure)
                {
                    return Result.Failure<string>(reversed.Error);
                }

                outbox.Enqueue(new Contracts.PayoutFailed(payout.Id, payout.Id, LegacyTutorId(payout), payout.Amount, payout.FailureReason!, true, payout.PayeeType, payout.PayeeId));
                Notify(payout, "payouts.payout.reversed", new Dictionary<string, string> { ["payoutId"] = payout.Id.ToString("D"), ["amount"] = payout.Amount.ToString(), ["reason"] = payout.FailureReason! });
                return "reversed";
            default:
                return "ignored";
        }
    }

    public async Task CompleteBatchIfDoneAsync(Guid batchId, DateTime now, CancellationToken ct)
    {
        var batch = await db.Batches.SingleOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null)
        {
            return;
        }

        var payouts = await db.Payouts.Where(p => p.BatchId == batchId).ToListAsync(ct);
        batch.TryComplete(payouts, now);
    }
}
