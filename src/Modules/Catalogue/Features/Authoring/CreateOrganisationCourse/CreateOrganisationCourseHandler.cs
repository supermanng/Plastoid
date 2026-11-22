using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Catalogue.Features.Authoring.CreateOrganisationCourse;

internal sealed class CreateOrganisationCourseHandler(CatalogueDbContext db, IOrganisationDirectory organisations, ICurrentUser currentUser, ICurrentTenant tenant) : ICommandHandler<CreateOrganisationCourseCommand, CreateOrganisationCourseResponse>
{
    public async Task<Result<CreateOrganisationCourseResponse>> Handle(CreateOrganisationCourseCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var organisation = await organisations.GetAsync(request.OrganisationId, cancellationToken);
        if (organisation is null)
        {
            return Error.NotFound("organisation_not_found", "No such organisation.");
        }

        if (!organisation.IsVerified)
        {
            return Error.Conflict("organisation_not_verified", "Courses can be created once the organisation's CAC and signatory are verified.");
        }

        var suffix = Guid.CreateVersion7().ToString("N")[^6..];
        var course = Course.CreateForOrganisation(tenant.TenantId, organisation.OrganisationId, userId, request.Title, request.Summary, request.Level, request.Language, request.Category, request.Skills, suffix);
        db.Courses.Add(course);
        return new CreateOrganisationCourseResponse(course.Id, course.Slug, course.Status.ToString().ToLowerInvariant());
    }
}
