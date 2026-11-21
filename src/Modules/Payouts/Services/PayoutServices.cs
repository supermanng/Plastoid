using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Payouts.Domain;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.Modules.Tutors.Contracts;
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

/// <summary>Journal entry 4 (and its reversal) of blueprint §18.</summary>
internal static class PayoutPostings
{
    /// <summary>Entry 4: tutor paid. Dr 2010 (tutor) / Cr 1020.</summary>
    public static JournalEntryDraft Entry4Settled(Payout payout, DateTime occurredAt)
        => new($"payout:{payout.Id:N}:settled", "tutor_payout", $"Payout {payout.ProviderReference} to {payout.AccountName}",
            [PostingDraft.Dr(AccountCodes.TutorPayable, payout.Amount, SubLedgerOwnerTypes.Tutor, payout.TutorUserId), PostingDraft.Cr(AccountCodes.BankClientFunds, payout.Amount)], occurredAt);

    /// <summary>Reversal: the bank returned the money. Dr 1020 / Cr 2010 (tutor).</summary>
    public static JournalEntryDraft Reversal(Payout payout, DateTime occurredAt)
        => new($"payout:{payout.Id:N}:reversed", "tutor_payout_reversal", $"Reversal of payout {payout.ProviderReference}",
            [PostingDraft.Dr(AccountCodes.BankClientFunds, payout.Amount), PostingDraft.Cr(AccountCodes.TutorPayable, payout.Amount, SubLedgerOwnerTypes.Tutor, payout.TutorUserId)], occurredAt);
}

public sealed record EarningsSummary(Money Gross, Money Commission, Money Net, Money Held, Money Pending, Money Paid, Money Payable, Money LedgerPayable, bool DisputeHold);

/// <summary>What a tutor can be paid now: released earnings less what was paid or is in flight, never more than the ledger's 2010 balance.</summary>
public sealed class PayableCalculator(PayoutsDbContext db, ILedgerService ledger)
{
    public async Task<EarningsSummary> SummariseAsync(Guid tutorUserId, DateTime now, CancellationToken ct)
    {
        var earnings = await db.Earnings.AsNoTracking().Where(e => e.TutorUserId == tutorUserId).ToListAsync(ct);
        var payouts = await db.Payouts.AsNoTracking().Where(p => p.TutorUserId == tutorUserId).ToListAsync(ct);
        var dispute = await db.Holds.AsNoTracking().AnyAsync(h => h.TutorUserId == tutorUserId && h.ReleasedAt == null && h.ReleasesAt > now, ct);
        var ledgerPayable = await ledger.GetBalanceAsync(AccountCodes.TutorPayable, tutorUserId, ct);
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

public sealed record SkippedTutor(Guid TutorUserId, string Reason, Money Payable);

public sealed record BatchDraft(PayoutBatch Batch, IReadOnlyList<Payout> Payouts, IReadOnlyList<SkippedTutor> Skipped);

/// <summary>Builds a payout batch (CreatePayoutBatch): minimum ₦5,000, name-matched account, no open dispute, payouts not frozen.</summary>
public sealed class PayoutRunner(PayoutsDbContext db, PayableCalculator calculator, ITutorPayoutAccounts accounts, IPayoutControls controls, IOptions<PayoutOptions> options, ICurrentTenant tenant)
{
    public async Task<Result<BatchDraft>> CreateBatchAsync(Guid makerId, DateTime now, CancellationToken ct)
    {
        if (await controls.ArePayoutsFrozenAsync(ct))
        {
            return Error.Conflict("payouts_frozen", "Payouts are frozen by the reconciliation control; resolve the shortfall first.");
        }

        var minimum = Money.NairaAmount(options.Value.MinimumPayoutNaira);
        var tutors = await db.Earnings.AsNoTracking().Where(e => e.Status == EarningStatus.Available).Select(e => e.TutorUserId).Distinct().ToListAsync(ct);
        var payouts = new List<Payout>();
        var skipped = new List<SkippedTutor>();
        var batchId = Entity.NewId();
        foreach (var tutor in tutors.OrderBy(t => t))
        {
            var summary = await calculator.SummariseAsync(tutor, now, ct);
            if (summary.Payable < minimum)
            {
                if (!summary.Payable.IsZero)
                {
                    skipped.Add(new SkippedTutor(tutor, "below_minimum", summary.Payable));
                }

                continue;
            }

            if (summary.DisputeHold)
            {
                skipped.Add(new SkippedTutor(tutor, "dispute_open", summary.Payable));
                continue;
            }

            var account = await accounts.GetAsync(tutor, ct);
            if (account is null)
            {
                skipped.Add(new SkippedTutor(tutor, "no_payout_account", summary.Payable));
                continue;
            }

            if (!account.NameMatched)
            {
                skipped.Add(new SkippedTutor(tutor, "name_mismatch", summary.Payable));
                continue;
            }

            payouts.Add(Payout.Create(tenant.TenantId, batchId, tutor, summary.Payable, account.BankCode, account.AccountNumberMasked, account.AccountName));
        }

        if (payouts.Count == 0)
        {
            return Error.Conflict("nothing_to_pay", skipped.Count == 0 ? "No tutor has a payable balance." : "Every payable tutor was skipped: " + string.Join(", ", skipped.Select(s => $"{s.TutorUserId:N}={s.Reason}")));
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

/// <summary>Applies a provider status to a payout: entry 4 on settlement, reversal entry when the bank returns the money.</summary>
internal sealed class PayoutSettlement(PayoutsDbContext db, ILedgerService ledger, IOutbox outbox)
{
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

                outbox.Enqueue(new Contracts.PayoutSettled(payout.Id, payout.Id, payout.TutorUserId, payout.Amount, payout.BankReference ?? string.Empty));
                outbox.Enqueue(NotificationRequested.To(payout.TutorUserId, "payouts.payout.settled", new Dictionary<string, string> { ["payoutId"] = payout.Id.ToString("D"), ["amount"] = payout.Amount.ToString(), ["bankRef"] = payout.BankReference ?? string.Empty }));
                return "settled";
            case "transfer.failed":
                var failed = payout.Fail(reason ?? "Transfer failed", now);
                if (failed.IsFailure)
                {
                    return Result.Failure<string>(failed.Error);
                }

                outbox.Enqueue(new Contracts.PayoutFailed(payout.Id, payout.Id, payout.TutorUserId, payout.Amount, payout.FailureReason!, false));
                outbox.Enqueue(NotificationRequested.To(payout.TutorUserId, "payouts.payout.failed", new Dictionary<string, string> { ["payoutId"] = payout.Id.ToString("D"), ["amount"] = payout.Amount.ToString(), ["reason"] = payout.FailureReason! }));
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

                outbox.Enqueue(new Contracts.PayoutFailed(payout.Id, payout.Id, payout.TutorUserId, payout.Amount, payout.FailureReason!, true));
                outbox.Enqueue(NotificationRequested.To(payout.TutorUserId, "payouts.payout.reversed", new Dictionary<string, string> { ["payoutId"] = payout.Id.ToString("D"), ["amount"] = payout.Amount.ToString(), ["reason"] = payout.FailureReason! }));
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
