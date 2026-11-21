using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Payouts.Domain;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.Modules.Payouts.Services;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;
using Quartz;

namespace Plastoids.Modules.Payouts.Jobs;

/// <summary>ComputePayableEarnings: every 15 minutes release earnings whose hold has passed and dispute holds past their 5 working days.</summary>
[DisallowConcurrentExecution]
public sealed class EarningsReleaseJob(PayoutsDbContext db, IUnitOfWork unitOfWork, IClock clock, ILogger<EarningsReleaseJob> logger) : IJob
{
    public const string JobName = "payouts-earnings-release";

    public async Task Execute(IJobExecutionContext context) => await RunAsync(clock.UtcNow, context.CancellationToken);

    public async Task<(int Released, int HoldsExpired)> RunAsync(DateTime now, CancellationToken ct)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await unitOfWork.BeginAsync(db, ct);
        var due = await db.Earnings.Where(e => e.Status == EarningStatus.Held && e.ReleasesAt != null && e.ReleasesAt <= now).Take(1000).ToListAsync(ct);
        var released = due.Count(e => e.Release(now));
        var holds = await db.Holds.Where(h => h.ReleasedAt == null && h.ReleasesAt <= now).ToListAsync(ct);
        foreach (var hold in holds)
        {
            hold.Release(Guid.Empty, "Auto-released after 5 working days (blueprint §18 review limit)", now);
        }

        await unitOfWork.CommitAsync(ct);
        if (released + holds.Count > 0)
        {
            logger.LogInformation("Payouts: {Released} earnings released, {Holds} dispute holds expired", released, holds.Count);
        }

        return (released, holds.Count);
    }
}

/// <summary>Weekly Thursday run (blueprint §18): the system drafts the batch as maker; a human checker approves it.</summary>
[DisallowConcurrentExecution]
public sealed class WeeklyPayoutRunJob(PayoutsDbContext db, IUnitOfWork unitOfWork, PayoutRunner runner, IClock clock, ILogger<WeeklyPayoutRunJob> logger) : IJob
{
    public const string JobName = "payouts-weekly-run";

    public async Task Execute(IJobExecutionContext context) => await RunAsync(clock.UtcNow, context.CancellationToken);

    public async Task<Guid?> RunAsync(DateTime now, CancellationToken ct)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await unitOfWork.BeginAsync(db, ct);
        var draft = await runner.CreateBatchAsync(Guid.Empty, now, ct);
        if (draft.IsFailure)
        {
            await unitOfWork.RollbackAsync(ct);
            logger.LogInformation("Weekly payout run: no batch ({Reason})", draft.Error.Code);
            return null;
        }

        await unitOfWork.CommitAsync(ct);
        logger.LogInformation("Weekly payout run: batch {Reference} with {Count} payouts totalling {Total} awaits approval", draft.Value.Batch.Reference, draft.Value.Batch.Count, draft.Value.Batch.Total);
        return draft.Value.Batch.Id;
    }
}
