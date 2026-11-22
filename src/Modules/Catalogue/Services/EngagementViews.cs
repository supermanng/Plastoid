using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Services;

/// <summary>Engagement terms on the wire: kind is revenue_share (shareBps) or per_enrolment (fixedFee).</summary>
public sealed record EngagementTermsView(string Kind, int ShareBps, Money? FixedFee);

/// <summary>ADR 0019 EngagementView: an organisation's offer to a tutor for one course, with its status and timestamps.</summary>
public sealed record EngagementView(
    Guid EngagementId,
    Guid CourseId,
    string CourseTitle,
    string CourseSlug,
    Guid OrganisationId,
    string OrganisationName,
    Guid TutorId,
    Guid TutorUserId,
    EngagementTermsView Terms,
    string Status,
    string? Note,
    Guid OfferedBy,
    DateTime OfferedAt,
    DateTime? RespondedAt,
    DateTime? EndedAt);

/// <summary>Shared EngagementView mapping (slices must not reference sibling slices): joins course title/slug and the organisation name.</summary>
internal sealed class EngagementViews(CatalogueDbContext db, IOrganisationDirectory organisations)
{
    public async Task<EngagementView> OneAsync(Engagement engagement, CancellationToken cancellationToken)
        => (await ListAsync([engagement], cancellationToken))[0];

    public async Task<IReadOnlyList<EngagementView>> ListAsync(IReadOnlyList<Engagement> engagements, CancellationToken cancellationToken)
    {
        if (engagements.Count == 0)
        {
            return [];
        }

        var courseIds = engagements.Select(e => e.CourseId).Distinct().ToList();
        var courses = await db.Courses.AsNoTracking().Where(c => courseIds.Contains(c.Id)).Select(c => new { c.Id, c.Title, c.Slug }).ToDictionaryAsync(c => c.Id, cancellationToken);
        var names = new Dictionary<Guid, string>();
        foreach (var organisationId in engagements.Select(e => e.OrganisationId).Distinct())
        {
            names[organisationId] = (await organisations.GetAsync(organisationId, cancellationToken))?.Name ?? string.Empty;
        }

        return engagements.Select(e => new EngagementView(
            e.Id,
            e.CourseId,
            courses.TryGetValue(e.CourseId, out var course) ? course.Title : string.Empty,
            course?.Slug ?? string.Empty,
            e.OrganisationId,
            names[e.OrganisationId],
            e.TutorId,
            e.TutorUserId,
            new EngagementTermsView(e.TermsKind, e.ShareBps, e.FixedFee),
            e.Status.ToString().ToLowerInvariant(),
            e.Note,
            e.OfferedBy,
            e.OfferedAt,
            e.RespondedAt,
            e.EndedAt)).ToList();
    }
}
