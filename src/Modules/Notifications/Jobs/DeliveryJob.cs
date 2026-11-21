using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Notifications.Domain;
using Plastoids.Modules.Notifications.Persistence;
using Plastoids.Modules.Notifications.Services;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;
using Quartz;

namespace Plastoids.Modules.Notifications.Jobs;

/// <summary>Every minute: send queued deliveries and deferred ones whose quiet hours (or retry back-off) have passed.</summary>
[DisallowConcurrentExecution]
public sealed class DeliveryJob(NotificationsDbContext db, IUnitOfWork unitOfWork, ChannelRouter router, IClock clock, ILogger<DeliveryJob> logger) : IJob
{
    public const string JobName = "notifications-delivery";

    public async Task Execute(IJobExecutionContext context) => await RunAsync(clock.UtcNow, context.CancellationToken);

    public async Task<(int Sent, int Failed)> RunAsync(DateTime now, CancellationToken ct)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await unitOfWork.BeginAsync(db, ct);
        var due = await db.Deliveries
            .Where(d => d.Status == DeliveryStatus.Queued || (d.Status == DeliveryStatus.Deferred && d.DeferredUntil <= now))
            .OrderBy(d => d.CreatedAt).Take(200).ToListAsync(ct);
        var sent = 0;
        var failed = 0;
        foreach (var delivery in due)
        {
            // Quiet hours may have been set after the message was queued.
            var preference = await db.Preferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == delivery.RecipientId, ct);
            if (preference?.QuietEndsAt(now) is { } quietEnd)
            {
                delivery.Defer(quietEnd);
                continue;
            }

            await router.SendAsync(delivery, ct);
            if (delivery.Status == DeliveryStatus.Sent)
            {
                sent++;
            }
            else if (delivery.Status is DeliveryStatus.Failed or DeliveryStatus.Deferred)
            {
                failed++;
            }
        }

        await unitOfWork.CommitAsync(ct);
        if (sent + failed > 0)
        {
            logger.LogInformation("Notifications delivery: {Sent} sent, {Failed} failed or retried", sent, failed);
        }

        return (sent, failed);
    }
}
