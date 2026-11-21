using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Catalogue.Features.Discovery.CompareCourses;

internal sealed class CompareCoursesHandler(CatalogueDbContext db) : IQueryHandler<CompareCoursesQuery, CompareCoursesResponse>
{
    public const int MaxCourses = 3;

    public async Task<Result<CompareCoursesResponse>> Handle(CompareCoursesQuery request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Distinct().ToList();
        if (ids.Count is 0 or > MaxCourses)
        {
            return Error.Validation("compare_ids_invalid", $"Compare between 1 and {MaxCourses} courses.");
        }

        var courses = await db.Courses.AsNoTracking().Where(c => ids.Contains(c.Id) && c.Status == CourseStatus.Published).ToListAsync(cancellationToken);
        var offerings = await db.Offerings.AsNoTracking().Where(o => o.Active && ids.Contains(o.CourseId)).ToListAsync(cancellationToken);
        var versions = await db.Versions.AsNoTracking().Where(v => ids.Contains(v.CourseId)).ToListAsync(cancellationToken);

        var rows = ids.Select(id => courses.SingleOrDefault(c => c.Id == id)).Where(c => c is not null).Select(c =>
        {
            var own = offerings.Where(o => o.CourseId == c!.Id).ToList();
            var version = versions.Where(v => v.CourseId == c!.Id && v.VersionNo == c.CurrentVersionNo).FirstOrDefault();
            return new CompareRow(c!.Id, c.Slug, c.Title, c.Level, own.Count == 0 ? null : own.Min(o => o.Price), own.Select(o => o.Format).Distinct().ToList(), own.Any(o => o.AcceptsSponsorship),
                c.EstimatedMinutes, version?.LessonCount ?? 0, c.RatingAverage, c.RatingCount, c.Skills);
        }).ToList();
        return new CompareCoursesResponse(rows);
    }
}
