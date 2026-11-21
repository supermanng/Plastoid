using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;

namespace Plastoids.Modules.Catalogue.Services;

internal sealed class CourseModeration(CatalogueDbContext db, IUnitOfWork unitOfWork) : ICourseModeration
{
    public async Task<IReadOnlyList<CourseModerationItem>> ListAsync(string? status, int limit, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        var parsed = !string.IsNullOrWhiteSpace(status) && Enum.TryParse<CourseStatus>(status, true, out var s) ? s : CourseStatus.InReview;
        return await db.Courses.AsNoTracking().Where(c => c.Status == parsed).OrderBy(c => c.SubmittedAt).Take(Math.Clamp(limit, 1, 200))
            .Select(c => new CourseModerationItem(c.Id, c.Title, c.Slug, c.OwnerUserId, c.Status.ToString().ToLowerInvariant(), c.SubmittedAt, c.ReviewNote, c.RatingAverage, c.RatingCount))
            .ToListAsync(cancellationToken);
    }
}
