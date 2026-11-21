using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Lms.Features.Cohorts.CreateCohort;

public sealed record CreateCohortCommand(Guid CourseId, Guid OfferingId, string Name, DateTime StartsAt, DateTime EndsAt, int Capacity, Guid? ProgrammeId) : ICommand<CreateCohortResponse>;

public sealed record CreateCohortResponse(Guid CohortId, Guid CourseId, Guid OfferingId, string Name, DateTime StartsAt, DateTime EndsAt, int Capacity);

internal sealed class CreateCohortValidator : AbstractValidator<CreateCohortCommand>
{
    public CreateCohortValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.OfferingId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Capacity).InclusiveBetween(1, 10_000);
        RuleFor(x => x).Must(x => x.EndsAt > x.StartsAt).WithMessage("endsAt must be after startsAt.");
    }
}

internal sealed class CreateCohortHandler(LmsDbContext db, ICourseOutlineReader courses, ICurrentUser currentUser, ICurrentTenant tenant) : ICommandHandler<CreateCohortCommand, CreateCohortResponse>
{
    public async Task<Result<CreateCohortResponse>> Handle(CreateCohortCommand request, CancellationToken cancellationToken)
    {
        var course = await courses.GetPublishedAsync(request.CourseId, null, cancellationToken);
        if (course is null)
        {
            return Error.NotFound("course_not_found", "No published course with that id.");
        }

        var userId = currentUser.UserId ?? Guid.Empty;
        if (course.OwnerUserId != userId && !currentUser.Roles.Contains("staff"))
        {
            return Error.Forbidden("not_course_tutor", "Only the course tutor (or staff) can open cohorts.");
        }

        var offering = course.Offerings.SingleOrDefault(o => o.OfferingId == request.OfferingId);
        if (offering is null || offering.Format != "cohort")
        {
            return Error.Validation("offering_not_cohort", "Cohorts need a cohort-format offering of this course.");
        }

        var created = Cohort.Create(tenant.TenantId, course.CourseId, offering.OfferingId, course.OwnerUserId, request.Name, request.StartsAt, request.EndsAt, request.Capacity, request.ProgrammeId);
        if (created.IsFailure)
        {
            return Result.Failure<CreateCohortResponse>(created.Error);
        }

        db.Cohorts.Add(created.Value);
        var c = created.Value;
        return new CreateCohortResponse(c.Id, c.CourseId, c.OfferingId, c.Name, c.StartsAt, c.EndsAt, c.Capacity);
    }
}

internal static class CreateCohortEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/", async (CreateCohortCommand command, ISender sender, CancellationToken ct) => (await sender.Send(command, ct)).ToCreatedResult(r => $"/api/v1/lms/cohorts/{r.CohortId}"))
            .WithName("CreateCohort")
            .WithSummary("Tutor: open a dated cohort with capacity on a cohort-format offering.")
            .RequireAuthorization(Policies.Tutor);
    }
}
