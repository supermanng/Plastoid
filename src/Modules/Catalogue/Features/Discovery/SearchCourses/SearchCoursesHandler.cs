using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Catalogue.Features.Discovery.SearchCourses;

internal sealed class SearchCoursesHandler(CatalogueDbContext db, IOrganisationDirectory organisations) : IQueryHandler<SearchCoursesQuery, SearchCoursesResponse>
{
    public async Task<Result<SearchCoursesResponse>> Handle(SearchCoursesQuery request, CancellationToken cancellationToken)
    {
        var limit = Cursor.ClampLimit(request.Limit);
        var courses = db.Courses.AsNoTracking().Where(c => c.Status == CourseStatus.Published);

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var q = request.Q.Trim();
            courses = courses.Where(c => EF.Functions.Match(new[] { c.Title, c.Summary }, q, MySqlMatchSearchMode.NaturalLanguage) > 0 || c.Title.Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(request.Level))
        {
            var level = request.Level.ToLowerInvariant();
            courses = courses.Where(c => c.Level == level);
        }

        if (!string.IsNullOrWhiteSpace(request.Lang))
        {
            var lang = request.Lang.ToLowerInvariant();
            courses = courses.Where(c => c.Language == lang);
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category.ToLowerInvariant();
            courses = courses.Where(c => c.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(request.Skill))
        {
            var skill = System.Text.Json.JsonSerializer.Serialize(request.Skill.Trim().ToLowerInvariant(), System.Text.Json.JsonSerializerOptions.Default);
            courses = courses.Where(c => EF.Functions.JsonContains(c.Skills, skill));
        }

        // Offering-level filters (funding, format, price) narrow through active offerings.
        var offerings = db.Offerings.AsNoTracking().Where(o => o.Active);
        var offeringFilter = false;
        if (!string.IsNullOrWhiteSpace(request.Funding))
        {
            var funding = System.Text.Json.JsonSerializer.Serialize(request.Funding.Trim().ToLowerInvariant(), System.Text.Json.JsonSerializerOptions.Default);
            offerings = offerings.Where(o => EF.Functions.JsonContains(o.FundingTypes, funding));
            offeringFilter = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Format))
        {
            var format = request.Format.Trim().ToLowerInvariant();
            offerings = offerings.Where(o => o.Format == format);
            offeringFilter = true;
        }

        if (request.MaxPriceKobo is { } max)
        {
            offerings = offerings.Where(o => o.Price.AmountKobo <= max);
            offeringFilter = true;
        }

        if (offeringFilter)
        {
            courses = courses.Where(c => offerings.Any(o => o.CourseId == c.Id));
        }

        var total = await courses.CountAsync(cancellationToken);
        if (Cursor.Decode(request.Cursor) is { } after)
        {
            courses = courses.Where(c => c.Id.CompareTo(after) > 0);
        }

        var rows = await courses.OrderBy(c => c.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;
        var page = rows.Take(limit).ToList();
        var ids = page.Select(c => c.Id).ToList();
        var pageOfferings = await db.Offerings.AsNoTracking().Where(o => o.Active && ids.Contains(o.CourseId)).ToListAsync(cancellationToken);
        var providers = await ProviderNamesAsync(page, cancellationToken);
        var items = page.Select(c => ToCard(c, pageOfferings.Where(o => o.CourseId == c.Id).ToList(), providers)).ToList();

        SearchSuggestions? suggestions = null;
        if (items.Count == 0)
        {
            // Zero-result page: relax every filter and surface what exists (story B1).
            var popular = await db.Courses.AsNoTracking().Where(c => c.Status == CourseStatus.Published).OrderByDescending(c => c.RatingCount).ThenByDescending(c => c.PublishedAt).Take(5).ToListAsync(cancellationToken);
            var popularIds = popular.Select(c => c.Id).ToList();
            var popularOfferings = await db.Offerings.AsNoTracking().Where(o => o.Active && popularIds.Contains(o.CourseId)).ToListAsync(cancellationToken);
            var allSkills = await db.Courses.AsNoTracking().Where(c => c.Status == CourseStatus.Published).Select(c => c.Skills).ToListAsync(cancellationToken);
            var categories = await db.Courses.AsNoTracking().Where(c => c.Status == CourseStatus.Published).Select(c => c.Category).Distinct().Take(10).ToListAsync(cancellationToken);
            IEnumerable<string> skillSuggestions = allSkills.SelectMany(s => s).GroupBy(s => s).OrderByDescending(g => g.Count()).Select(g => g.Key);
            if (!string.IsNullOrWhiteSpace(request.Q) || !string.IsNullOrWhiteSpace(request.Skill))
            {
                var needle = (request.Skill ?? request.Q)!.Trim().ToLowerInvariant();
                skillSuggestions = skillSuggestions.OrderByDescending(s => s.Contains(needle, StringComparison.Ordinal) || needle.Contains(s, StringComparison.Ordinal) ? 1 : 0);
            }

            var popularProviders = await ProviderNamesAsync(popular, cancellationToken);
            suggestions = new SearchSuggestions(skillSuggestions.Take(8).ToList(), categories, popular.Select(c => ToCard(c, popularOfferings.Where(o => o.CourseId == c.Id).ToList(), popularProviders)).ToList());
        }

        return new SearchCoursesResponse(items, hasMore ? Cursor.Encode(items[^1].CourseId) : null, total, suggestions);
    }

    /// <summary>Organisation names for the page's organisation-owned courses, one directory lookup per distinct organisation (ADR 0019).</summary>
    private async Task<IReadOnlyDictionary<Guid, string>> ProviderNamesAsync(IEnumerable<Course> courses, CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, string>();
        foreach (var organisationId in courses.Where(c => c.IsOrganisationOwned && c.OrganisationId is not null).Select(c => c.OrganisationId!.Value).Distinct())
        {
            if (await organisations.GetAsync(organisationId, cancellationToken) is { } organisation)
            {
                names[organisationId] = organisation.Name;
            }
        }

        return names;
    }

    internal static CourseCard ToCard(Course c, IReadOnlyList<Offering> offerings, IReadOnlyDictionary<Guid, string> providers) => new(
        c.Id,
        c.Slug,
        c.Title,
        c.Summary.Length > 240 ? c.Summary[..240] + "…" : c.Summary,
        c.Level,
        c.Language,
        c.Category,
        c.Skills,
        offerings.Count == 0 ? null : offerings.Min(o => o.Price),
        offerings.Select(o => o.Format).Distinct().ToList(),
        offerings.Any(o => o.AcceptsSponsorship),
        c.RatingAverage,
        c.RatingCount,
        c.EstimatedMinutes,
        c.OrganisationId is { } organisationId && providers.TryGetValue(organisationId, out var provider) ? provider : null);
}
