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

        db.Offerings.Add(created.Value);
        return new CreateOfferingResponse(created.Value.Id, course.Id, created.Value.Format, created.Value.Price);
    }
}
