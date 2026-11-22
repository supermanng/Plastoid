using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Catalogue.Features.Authoring.ListOrganisationCourses;

internal sealed class ListOrganisationCoursesHandler(CatalogueDbContext db, CourseSummaries summaries) : IQueryHandler<ListOrganisationCoursesQuery, IReadOnlyList<CourseSummary>>
{
    public async Task<Result<IReadOnlyList<CourseSummary>>> Handle(ListOrganisationCoursesQuery request, CancellationToken cancellationToken)
        => Result.Success(await summaries.ListAsync(db.Courses.Where(c => c.OwnerType == CourseOwnerType.Organisation && c.OrganisationId == request.OrganisationId), cancellationToken));
}
