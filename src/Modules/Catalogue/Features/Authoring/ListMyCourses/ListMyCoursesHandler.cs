using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Catalogue.Features.Authoring.ListMyCourses;

internal sealed class ListMyCoursesHandler(CatalogueDbContext db, CourseSummaries summaries, ICurrentUser currentUser) : IQueryHandler<ListMyCoursesQuery, IReadOnlyList<CourseSummary>>
{
    public async Task<Result<IReadOnlyList<CourseSummary>>> Handle(ListMyCoursesQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        return Result.Success(await summaries.ListAsync(db.Courses.Where(c => c.OwnerUserId == userId), cancellationToken));
    }
}
