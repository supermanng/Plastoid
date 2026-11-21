using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Tutors.Features.Discovery.ListTutorsForDiscovery;

internal sealed class ListTutorsForDiscoveryHandler(TutorsDbContext db) : IQueryHandler<ListTutorsForDiscoveryQuery, Page<TutorCard>>
{
    public async Task<Result<Page<TutorCard>>> Handle(ListTutorsForDiscoveryQuery request, CancellationToken cancellationToken)
    {
        var limit = Cursor.ClampLimit(request.Limit);
        var query = db.Profiles.AsNoTracking().Where(p => p.Status == TutorStatus.Verified);

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var q = request.Q.Trim();
            query = query.Where(p => EF.Functions.Match(new[] { p.Headline, p.Bio }, q, MySqlMatchSearchMode.NaturalLanguage) > 0 || p.Headline.Contains(q));
        }

        if (request.MaxRateKobo is { } max)
        {
            query = query.Where(p => p.HourlyRate.AmountKobo <= max);
        }

        // JSON array filters: MySQL JSON_CONTAINS through a raw predicate keeps the index-less scan cheap at MVP sizes.
        if (!string.IsNullOrWhiteSpace(request.Skill))
        {
            var skill = request.Skill.Trim().ToLowerInvariant();
            query = query.Where(p => EF.Functions.JsonContains(p.Subjects, System.Text.Json.JsonSerializer.Serialize(skill, System.Text.Json.JsonSerializerOptions.Default)));
        }

        if (!string.IsNullOrWhiteSpace(request.Lang))
        {
            var lang = request.Lang.Trim().ToLowerInvariant();
            query = query.Where(p => EF.Functions.JsonContains(p.Languages, System.Text.Json.JsonSerializer.Serialize(lang, System.Text.Json.JsonSerializerOptions.Default)));
        }

        if (!string.IsNullOrWhiteSpace(request.Format))
        {
            var format = request.Format.Trim().ToLowerInvariant();
            query = query.Where(p => EF.Functions.JsonContains(p.Formats, System.Text.Json.JsonSerializer.Serialize(format, System.Text.Json.JsonSerializerOptions.Default)));
        }

        if (Cursor.Decode(request.Cursor) is { } after)
        {
            query = query.Where(p => p.Id.CompareTo(after) > 0);
        }

        // Rank: quality first, then newest verified tutors get exploration exposure through the id order.
        var rows = await query.OrderByDescending(p => p.Level).ThenByDescending(p => p.QualityScore).ThenBy(p => p.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;
        var items = rows.Take(limit).Select(p => new TutorCard(p.Id, p.Headline, p.Level.ToString().ToLowerInvariant(), p.Subjects, p.Languages, p.Formats, p.HourlyRate, p.RatingAverage, p.RatingCount, p.TimeZone)).ToList();
        return new Page<TutorCard>(items, hasMore ? Cursor.Encode(items[^1].TutorId) : null);
    }
}
