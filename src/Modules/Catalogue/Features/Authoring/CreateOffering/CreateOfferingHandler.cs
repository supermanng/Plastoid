using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Catalogue.Features.Authoring.CreateOffering;

internal sealed class CreateOfferingHandler(CatalogueDbContext db, CourseAccess access, IClock clock) : ICommandHandler<CreateOfferingCommand, CreateOfferingResponse>
{
    public async Task<Result<CreateOfferingResponse>> Handle(CreateOfferingCommand request, CancellationToken cancellationToken)
    {
        var owned = await access.OwnedAsync(request.CourseId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<CreateOfferingResponse>(owned.Error);
        }

        var course = owned.Value;
        var created = Offering.Create(course.TenantId, course.Id, request.Format, request.Price, request.FundingTypes, request.Capacity, request.StartAt, request.DurationWeeks, clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<CreateOfferingResponse>(created.Error);
        }

        var offering = created.Value;
        if (course.IsOrganisationOwned)
        {
            // ADR 0019 §4: who teaches an organisation course's offering is fixed at the offering, from an active engagement.
            if (request.TutorUserId is not { } tutorUserId)
            {
                if (request.Format != OfferingFormats.SelfPaced)
                {
                    return Error.Validation("tutor_required", "Cohort and one-to-one offerings of an organisation course need an engaged tutor (tutorUserId).");
                }
            }
            else
            {
                var engagement = await db.Engagements.AsNoTracking()
                    .SingleOrDefaultAsync(e => e.CourseId == course.Id && e.TutorUserId == tutorUserId && e.Status == EngagementStatus.Active, cancellationToken);
                if (engagement is null)
                {
                    return Error.Validation("tutor_not_engaged", "The tutor has no active engagement on this course; offer one and wait for them to accept.");
                }

                offering.AssignTutor(tutorUserId, engagement.Id);
            }
        }

        db.Offerings.Add(offering);
        return new CreateOfferingResponse(offering.Id, course.Id, offering.Format, offering.Price, offering.TutorUserId, offering.EngagementId);
    }
}
