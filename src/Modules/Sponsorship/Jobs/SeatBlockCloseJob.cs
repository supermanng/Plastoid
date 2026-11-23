using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;
using Quartz;

namespace Plastoids.Modules.Sponsorship.Jobs;

/// <summary>
/// ADR 0021 §4 close-out: once a cohort has ended, every active block in it releases its open seats (units back to the wallet,
/// entry 16 with reference suffix "closeout") and closes. Runs nightly; each block is handled in its own transaction.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class SeatBlockCloseJob(SponsorshipDbContext db, IUnitOfWork unitOfWork, SeatReleaser releaser, IClock clock, ILogger<SeatBlockCloseJob> logger) : IJob
{
    public const string JobName = "sponsorship-seat-block-close";

    public async Task Execute(IJobExecutionContext context) => await RunAsync(clock.UtcNow, context.CancellationToken);

    /// <summary>Closes every active block whose cohort ended before <paramref name="now"/>; returns how many were closed.</summary>
    public async Task<int> RunAsync(DateTime now, CancellationToken ct)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        var due = await db.SeatBlocks.AsNoTracking().Where(b => b.Status == SeatBlockStatus.Active && b.EndsAt < now).Select(b => b.Id).ToListAsync(ct);
        var closed = 0;
        foreach (var blockId in due)
        {
            await unitOfWork.BeginAsync(db, ct);
            var block = await db.SeatBlocks.SingleAsync(b => b.Id == blockId, ct);
            if (block.Open > 0)
            {
                var released = await releaser.ReleaseAsync(block, block.Open, now, ct, SeatReleaser.CloseoutSuffix);
                if (released.IsFailure)
                {
                    throw new InvalidOperationException($"Seat block {block.Id} close-out release failed: {released.Error.Code} {released.Error.Message}");
                }
            }

            block.Close(now);
            await unitOfWork.CommitAsync(ct);
            closed++;
        }

        if (closed > 0)
        {
            logger.LogInformation("Seat block close-out: {Blocks} blocks closed", closed);
        }

        return closed;
    }
}
