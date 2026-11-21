using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Applications.Domain;
using Plastoids.Modules.Applications.Persistence;
using Plastoids.Modules.Applications.Services;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;
using Quartz;

namespace Plastoids.Modules.Applications.Jobs;

/// <summary>Hourly: expire lapsed offers and unpaid co-pays, then backfill open seats from each waitlist (story F4).</summary>
[DisallowConcurrentExecution]
public sealed class WaitlistBackfillJob(ApplicationsDbContext db, IUnitOfWork unitOfWork, OfferService offers, IClock clock, ILogger<WaitlistBackfillJob> logger) : IJob
{
    public const string JobName = "applications-waitlist-backfill";

    public async Task Execute(IJobExecutionContext context) => await RunAsync(clock.UtcNow, context.CancellationToken);

    public async Task<(int Expired, int Offered)> RunAsync(DateTime now, CancellationToken ct)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await unitOfWork.BeginAsync(db, ct);
        var expired = await offers.ExpireAsync(now, ct);
        await db.SaveChangesAsync(ct);
        var programmeIds = await db.Applications.Where(a => a.Status == ApplicationStatus.Waitlisted).Select(a => a.ProgrammeId).Distinct().ToListAsync(ct);
        var offered = 0;
        foreach (var programmeId in programmeIds)
        {
            offered += await offers.BackfillAsync(programmeId, now, ct);
        }

        await unitOfWork.CommitAsync(ct);
        if (expired + offered > 0)
        {
            logger.LogInformation("Waitlist backfill: {Expired} offers expired, {Offered} new offers", expired, offered);
        }

        return (expired, offered);
    }
}
