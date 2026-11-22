using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Ledger.Domain;
using Plastoids.Modules.Ledger.Persistence;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;
using Quartz;

namespace Plastoids.Modules.Ledger.Jobs;

/// <summary>Nightly: every journal entry must net to zero. Any that does not raises ledger.integrity-alert.v1.</summary>
[DisallowConcurrentExecution]
public sealed class BalanceVerificationJob(LedgerDbContext db, IUnitOfWork unitOfWork, IOutbox outbox, IClock clock, ILogger<BalanceVerificationJob> logger) : IJob
{
    public const string JobName = "ledger-balance-verification";

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        var unbalanced = await db.Postings.AsNoTracking()
            .GroupBy(p => p.JournalEntryId)
            .Select(g => new { EntryId = g.Key, Net = g.Sum(p => p.DebitKobo) - g.Sum(p => p.CreditKobo) })
            .Where(x => x.Net != 0)
            .ToListAsync(ct);

        await unitOfWork.BeginAsync(db, ct);
        var control = await db.Controls.SingleOrDefaultAsync(c => c.Id == LedgerControl.SingletonId, ct);
        if (control is null)
        {
            control = LedgerControl.Create(Tenants.Marketplace);
            db.Controls.Add(control);
        }

        control.RecordVerification(clock.UtcNow, unbalanced.Count);
        foreach (var bad in unbalanced)
        {
            logger.LogError("Journal entry {EntryId} is unbalanced by {Net} kobo", bad.EntryId, bad.Net);
            outbox.Enqueue(new IntegrityAlert(bad.EntryId, bad.EntryId, bad.Net));
        }

        await unitOfWork.CommitAsync(ct);
    }
}

/// <summary>
/// Daily: client bank balance + gateway in transit ≥ tutor payable + programme funds held + refund credits
/// (blueprint §18). Shortfall → payouts_frozen + ledger.shortfall-detected.v1.
/// </summary>
[DisallowConcurrentExecution]
public sealed class ReconciliationJob(LedgerDbContext db, ILedgerService ledger, IUnitOfWork unitOfWork, IOutbox outbox, IBankStatement bank, IGatewaySettlementReport gateway, IClock clock, ILogger<ReconciliationJob> logger) : IJob
{
    public const string JobName = "ledger-reconciliation";

    public async Task Execute(IJobExecutionContext context) => await RunAsync(context.CancellationToken);

    public async Task<(long Assets, long Liabilities)> RunAsync(CancellationToken ct)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var ledgerClientFunds = await ledger.GetBalanceAsync(AccountCodes.BankClientFunds, null, ct);
        var ledgerInTransit = await ledger.GetBalanceAsync(AccountCodes.GatewayReceivable, null, ct);
        var bankBalance = await bank.GetClientFundsBalanceAsync(today, ct) ?? ledgerClientFunds;
        var inTransit = await gateway.GetInTransitAsync(today, ct) ?? ledgerInTransit;

        var liabilities = (await ledger.GetBalanceAsync(AccountCodes.TutorPayable, null, ct)).AmountKobo
            + (await ledger.GetBalanceAsync(AccountCodes.ProgrammeFundsHeld, null, ct)).AmountKobo
            + (await ledger.GetBalanceAsync(AccountCodes.LearnerRefundCredit, null, ct)).AmountKobo
            + (await ledger.GetBalanceAsync(AccountCodes.UnitBalancesHeld, null, ct)).AmountKobo;
        var assets = bankBalance.AmountKobo + inTransit.AmountKobo;

        await unitOfWork.BeginAsync(db, ct);
        var control = await db.Controls.SingleOrDefaultAsync(c => c.Id == LedgerControl.SingletonId, ct);
        if (control is null)
        {
            control = LedgerControl.Create(Tenants.Marketplace);
            db.Controls.Add(control);
        }

        var wasFrozen = control.PayoutsFrozen;
        control.RecordReconciliation(clock.UtcNow, assets, liabilities);
        if (assets < liabilities && !wasFrozen)
        {
            logger.LogError("Client-funds shortfall: assets {Assets} < liabilities {Liabilities} kobo; payouts frozen", assets, liabilities);
            outbox.Enqueue(new ShortfallDetected(LedgerControl.SingletonId, today, assets, liabilities, liabilities - assets));
        }

        await unitOfWork.CommitAsync(ct);
        return (assets, liabilities);
    }
}
