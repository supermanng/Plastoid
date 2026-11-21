using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Features.Opportunities.ListOpportunities;

/// <summary>Story F2: published programmes still accepting applications, with the co-pay (if any) shown before applying.</summary>
public sealed record ListOpportunitiesQuery(string? Cursor, int? Limit) : IQuery<Page<OpportunityView>>;

public sealed record OpportunityView(Guid ProgrammeId, string Name, string SponsorName, Guid CourseId, string? CourseTitle, int Seats, int SeatsOpen, int SponsorSharePercent, Money CoPayPerSeat, bool HasEntryAssessment, DateTime? ApplicationsCloseAt, DateTime? StartsAt, DateTime? EndsAt, JsonElement EligibilityRules);

internal sealed class ListOpportunitiesValidator : AbstractValidator<ListOpportunitiesQuery>
{
    public ListOpportunitiesValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, Cursor.MaxLimit).When(x => x.Limit is not null);
    }
}

internal sealed class ListOpportunitiesHandler(SponsorshipDbContext db, IClock clock) : IQueryHandler<ListOpportunitiesQuery, Page<OpportunityView>>
{
    public async Task<Result<Page<OpportunityView>>> Handle(ListOpportunitiesQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var limit = Cursor.ClampLimit(request.Limit);
        var after = Cursor.Decode(request.Cursor);
        var query = db.Programmes.AsNoTracking().Where(p => p.Status == ProgrammeStatus.Published && p.ApplicationsCloseAt > now);
        if (after is { } cursor)
        {
            query = query.Where(p => p.Id.CompareTo(cursor) > 0);
        }

        var programmes = await query.OrderBy(p => p.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var ids = programmes.Select(p => p.Id).ToList();
        var open = await db.Seats.AsNoTracking().Where(s => ids.Contains(s.ProgrammeId) && s.Status == SeatStatus.Open).GroupBy(s => s.ProgrammeId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var items = programmes.Take(limit).Select(p =>
        {
            using var rules = JsonDocument.Parse(string.IsNullOrWhiteSpace(p.EligibilityRulesJson) ? "{}" : p.EligibilityRulesJson);
            return new OpportunityView(p.Id, p.Name, p.SponsorName, p.CourseId ?? Guid.Empty, p.CourseTitle, p.SeatsUnlocked, open.GetValueOrDefault(p.Id), p.SponsorSharePercent, p.CoPayPerSeat, p.EntryAssessmentJson is not null, p.ApplicationsCloseAt, p.StartsAt, p.EndsAt, rules.RootElement.Clone());
        }).ToList();
        return new Page<OpportunityView>(items, programmes.Count > limit ? Cursor.Encode(items[^1].ProgrammeId) : null);
    }
}

internal static class ListOpportunitiesEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (string? cursor, int? limit, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListOpportunitiesQuery(cursor, limit), ct)).ToHttpResult())
            .WithName("ListOpportunities")
            .WithSummary("Public: sponsored opportunities open for applications.")
            .AllowAnonymous();
    }
}
