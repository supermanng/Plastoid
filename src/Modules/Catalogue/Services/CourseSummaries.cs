using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;

namespace Plastoids.Modules.Catalogue.Services;

/// <summary>ADR 0019 CourseSummary: an authoring-side row for the organisation's and the tutor's course lists.</summary>
public sealed record CourseSummary(
    Guid CourseId,
    string Slug,
    string Title,
    string Status,
    string Level,
    string Category,
    string Language,
    string OwnerType,
    Guid? OrganisationId,
    int CurrentVersionNo,
    DateTime? PublishedAt,
    DateTime UpdatedAt,
    int OfferingCount,
    int ActiveEngagements);

/// <summary>Shared CourseSummary mapping (slices must not reference sibling slices): counts active offerings and active engagements per course.</summary>
internal sealed class CourseSummaries(CatalogueDbContext db)
{
    public async Task<IReadOnlyList<CourseSummary>> ListAsync(IQueryable<Course> courses, CancellationToken cancellationToken)
    {
        var rows = await courses.AsNoTracking().OrderByDescending(c => c.UpdatedAt).Take(500).ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(c => c.Id).ToList();
        var offerings = await db.Offerings.AsNoTracking().Where(o => ids.Contains(o.CourseId) && o.Active).GroupBy(o => o.CourseId).Select(g => new { CourseId = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.CourseId, g => g.Count, cancellationToken);
        var engagements = await db.Engagements.AsNoTracking().Where(e => ids.Contains(e.CourseId) && e.Status == EngagementStatus.Active).GroupBy(e => e.CourseId).Select(g => new { CourseId = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.CourseId, g => g.Count, cancellationToken);
        return rows.Select(c => new CourseSummary(c.Id, c.Slug, c.Title, c.Status.ToString().ToLowerInvariant(), c.Level, c.Category, c.Language, c.OwnerTypeName, c.OrganisationId,
            c.CurrentVersionNo, c.PublishedAt, c.UpdatedAt, offerings.GetValueOrDefault(c.Id), engagements.GetValueOrDefault(c.Id))).ToList();
    }
}
