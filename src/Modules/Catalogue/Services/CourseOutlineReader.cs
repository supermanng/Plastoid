using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Catalogue.Persistence;

namespace Plastoids.Modules.Catalogue.Services;

internal sealed class CourseOutlineReader(CatalogueDbContext db, IUnitOfWork unitOfWork) : ICourseOutlineReader
{
    public async Task<PublishedCourse?> GetPublishedAsync(Guid courseId, int? versionNo, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == courseId, cancellationToken);
        if (course is null || course.CurrentVersionNo == 0)
        {
            return null;
        }

        var version = await db.Versions.AsNoTracking().SingleOrDefaultAsync(v => v.CourseId == courseId && v.VersionNo == (versionNo ?? course.CurrentVersionNo), cancellationToken);
        if (version is null)
        {
            return null;
        }

        var outline = OutlineSnapshot.Deserialise(version.OutlineJson);
        var lessons = outline.OrderBy(m => m.Order).SelectMany(m => m.Lessons.OrderBy(l => l.Order).Select(l => new PublishedLesson(l.Id, m.Id, l.Type, l.Title, l.Order, l.DurationMinutes, l.IsPreview, l.PlaybackId, l.Content))).ToList();
        var offerings = await db.Offerings.AsNoTracking().Where(o => o.CourseId == courseId && o.Active).Select(o => new PublishedOffering(o.Id, o.Format, o.DurationWeeks, o.Capacity, o.StartAt)).ToListAsync(cancellationToken);
        return new PublishedCourse(course.Id, course.Slug, course.Title, course.OwnerUserId, version.VersionNo, lessons, offerings);
    }

    public async Task<Guid?> FindCourseForOfferingAsync(Guid offeringId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        return await db.Offerings.AsNoTracking().Where(o => o.Id == offeringId).Select(o => (Guid?)o.CourseId).SingleOrDefaultAsync(cancellationToken);
    }

    private async Task EnlistAsync(CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }
    }
}
