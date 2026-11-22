using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Catalogue.Features.Engagements.OfferEngagement;

internal sealed class OfferEngagementHandler(CatalogueDbContext db, EngagementViews views, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<OfferEngagementCommand, EngagementView>
{
    public async Task<Result<EngagementView>> Handle(OfferEngagementCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == request.CourseId && c.OwnerType == CourseOwnerType.Organisation && c.OrganisationId == request.OrganisationId, cancellationToken);
        if (course is null)
        {
            return Error.NotFound("course_not_found", "The organisation has no such course.");
        }

        var tutor = await db.VerifiedTutors.AsNoTracking().SingleOrDefaultAsync(t => t.TutorId == request.TutorId, cancellationToken);
        if (tutor is null)
        {
            return Error.NotFound("tutor_not_found", "No verified tutor with that id.");
        }

        if (await db.Engagements.AnyAsync(e => e.CourseId == course.Id && e.TutorUserId == tutor.UserId && (e.Status == EngagementStatus.Offered || e.Status == EngagementStatus.Active), cancellationToken))
        {
            return Error.Conflict("engagement_exists", "This tutor already has an offered or active engagement on the course.");
        }

        var terms = new EngagementTerms(request.Terms.Kind, request.Terms.ShareBps ?? 0, request.Terms.FixedFee);
        var offered = Engagement.Offer(course.TenantId, course.Id, course.OrganisationId!.Value, tutor.TutorId, tutor.UserId, terms, request.Note, userId, clock.UtcNow);
        if (offered.IsFailure)
        {
            return Result.Failure<EngagementView>(offered.Error);
        }

        db.Engagements.Add(offered.Value);
        var view = await views.OneAsync(offered.Value, cancellationToken);
        outbox.Enqueue(NotificationRequested.To(tutor.UserId, "catalogue.engagement.offered", new Dictionary<string, string>
        {
            ["engagementId"] = view.EngagementId.ToString("D"),
            ["courseTitle"] = view.CourseTitle,
            ["organisationName"] = view.OrganisationName,
        }));
        return view;
    }
}
