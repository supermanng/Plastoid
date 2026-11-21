using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Tutors.Contracts;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;
using Quartz;

namespace Plastoids.Modules.Tutors.Jobs;

/// <summary>Every 10 minutes: queue items past their SLA raise tutors.review-sla-breached.v1 (once) through the outbox.</summary>
[DisallowConcurrentExecution]
public sealed class ReviewSlaJob(TutorsDbContext db, IUnitOfWork unitOfWork, IOutbox outbox, IClock clock, ILogger<ReviewSlaJob> logger) : IJob
{
    public const string JobName = "tutors-review-sla";

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        var now = clock.UtcNow;
        var overdue = await db.ReviewQueue.Where(q => q.CompletedAt == null && q.SlaAlertedAt == null && q.DueAt < now).Take(100).ToListAsync(ct);
        if (overdue.Count == 0)
        {
            return;
        }

        await unitOfWork.BeginAsync(db, ct);
        foreach (var item in overdue)
        {
            item.MarkAlerted(now);
            outbox.Enqueue(new ReviewSlaBreached(item.TutorProfileId, item.Id, item.TutorProfileId, item.Kind, item.DueAt));
        }

        await unitOfWork.CommitAsync(ct);
        logger.LogWarning("{Count} tutor review item(s) breached their SLA", overdue.Count);
    }
}
