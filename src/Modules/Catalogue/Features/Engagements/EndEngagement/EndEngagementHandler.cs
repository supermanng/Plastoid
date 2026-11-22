using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Catalogue.Features.Engagements.EndEngagement;

internal sealed class EndEngagementHandler(CatalogueDbContext db, CourseAccess access, EngagementViews views, IOutbox outbox, IClock clock) : ICommandHandler<EndEngagementCommand, EngagementView>
{
    public async Task<Result<EngagementView>> Handle(EndEngagementCommand request, CancellationToken cancellationToken)
    {
        var engagement = await db.Engagements.SingleOrDefaultAsync(e => e.Id == request.EngagementId, cancellationToken);
        if (engagement is null)
        {
            return Error.NotFound("engagement_not_found", "No such engagement.");
        }

        var owned = await access.OwnedAsync(engagement.CourseId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<EngagementView>(owned.Error);
        }

        var ended = engagement.End(request.Reason, clock.UtcNow);
        if (ended.IsFailure)
        {
            return Result.Failure<EngagementView>(ended.Error);
        }

        var view = await views.OneAsync(engagement, cancellationToken);
        outbox.Enqueue(NotificationRequested.To(engagement.TutorUserId, "catalogue.engagement.ended", new Dictionary<string, string>
        {
            ["engagementId"] = view.EngagementId.ToString("D"),
            ["courseTitle"] = view.CourseTitle,
            ["organisationName"] = view.OrganisationName,
        }));
        return view;
    }
}
