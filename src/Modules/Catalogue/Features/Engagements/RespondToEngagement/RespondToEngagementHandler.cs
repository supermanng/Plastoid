using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Catalogue.Features.Engagements.RespondToEngagement;

internal sealed class RespondToEngagementHandler(CatalogueDbContext db, EngagementViews views, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<RespondToEngagementCommand, EngagementView>
{
    public async Task<Result<EngagementView>> Handle(RespondToEngagementCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var engagement = await db.Engagements.SingleOrDefaultAsync(e => e.Id == request.EngagementId, cancellationToken);
        if (engagement is null)
        {
            return Error.NotFound("engagement_not_found", "No such engagement.");
        }

        if (engagement.TutorUserId != userId)
        {
            return Error.Forbidden("not_engagement_tutor", "Only the tutor this engagement was offered to can respond.");
        }

        var now = clock.UtcNow;
        var responded = request.Accept ? engagement.Accept(request.Note, now) : engagement.Decline(request.Note, now);
        if (responded.IsFailure)
        {
            return Result.Failure<EngagementView>(responded.Error);
        }

        var view = await views.OneAsync(engagement, cancellationToken);
        outbox.Enqueue(NotificationRequested.To(engagement.OfferedBy, request.Accept ? "catalogue.engagement.accepted" : "catalogue.engagement.declined", new Dictionary<string, string>
        {
            ["engagementId"] = view.EngagementId.ToString("D"),
            ["courseTitle"] = view.CourseTitle,
        }));
        return view;
    }
}
