using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;

namespace Plastoids.Modules.Catalogue.Services;

/// <summary>Read side of tutor engagements for Lms and Payments (ADR 0019). Joins the caller's unit of work when one is open.</summary>
internal sealed class EngagementReader(CatalogueDbContext db, IUnitOfWork unitOfWork) : IEngagementReader
{
    public async Task<IReadOnlyList<ActiveEngagement>> ActiveForCourseAsync(Guid courseId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var rows = await db.Engagements.AsNoTracking().Where(e => e.CourseId == courseId && e.Status == EngagementStatus.Active).ToListAsync(cancellationToken);
        return rows.Select(ToActive).ToList();
    }

    public async Task<ActiveEngagement?> GetActiveAsync(Guid engagementId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var row = await db.Engagements.AsNoTracking().SingleOrDefaultAsync(e => e.Id == engagementId && e.Status == EngagementStatus.Active, cancellationToken);
        return row is null ? null : ToActive(row);
    }

    public async Task<bool> IsTeachingAsync(Guid courseId, Guid userId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        return await db.Engagements.AsNoTracking().AnyAsync(e => e.CourseId == courseId && e.TutorUserId == userId && e.Status == EngagementStatus.Active, cancellationToken);
    }

    internal static ActiveEngagement ToActive(Engagement e) => new(e.Id, e.CourseId, e.OrganisationId, e.TutorUserId, e.TutorId, e.ToTerms());

    private async Task EnlistAsync(CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }
    }
}
