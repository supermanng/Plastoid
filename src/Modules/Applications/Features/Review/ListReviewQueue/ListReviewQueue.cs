using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Applications.Domain;
using Plastoids.Modules.Applications.Persistence;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Applications.Features.Review.ListReviewQueue;

/// <summary>Story F4 reviewer queue, ordered by total score. blind=true hides identity fields and the learner id.</summary>
public sealed record ListReviewQueueQuery(Guid ProgrammeId, string? Status, bool Blind, string? Cursor, int? Limit) : IQuery<ReviewQueueView>;

public sealed record ReviewQueueItem(Guid ApplicationId, Guid? LearnerId, string Status, DateTime? SubmittedAt, bool Flagged, string? FlagReason, decimal? AssessmentScore, bool? AssessmentPassed, decimal? RubricScore, decimal? TotalScore, Guid? SeatId, DateTime? OfferExpiresAt, JsonElement Answers);

public sealed record ReviewQueueView(Guid ProgrammeId, int Seats, int SeatsOpen, bool ShortlistApproved, IReadOnlyDictionary<string, int> CountsByStatus, Page<ReviewQueueItem> Page);

internal sealed class ListReviewQueueValidator : AbstractValidator<ListReviewQueueQuery>
{
    public ListReviewQueueValidator()
    {
        RuleFor(x => x.ProgrammeId).NotEmpty();
        RuleFor(x => x.Status).Must(s => Enum.TryParse<ApplicationStatus>(s, true, out _)).When(x => x.Status is not null).WithMessage("Unknown status.");
        RuleFor(x => x.Limit).InclusiveBetween(1, Cursor.MaxLimit).When(x => x.Limit is not null);
    }
}

internal sealed class ListReviewQueueHandler(ApplicationsDbContext db, IProgrammeReader programmes) : IQueryHandler<ListReviewQueueQuery, ReviewQueueView>
{
    public async Task<Result<ReviewQueueView>> Handle(ListReviewQueueQuery request, CancellationToken cancellationToken)
    {
        var programme = await programmes.GetAsync(request.ProgrammeId, cancellationToken);
        if (programme is null)
        {
            return Error.NotFound("programme_not_found", "No such programme.");
        }

        var counts = await db.Applications.AsNoTracking().Where(a => a.ProgrammeId == programme.ProgrammeId).GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var query = db.Applications.AsNoTracking().Where(a => a.ProgrammeId == programme.ProgrammeId && a.Status != ApplicationStatus.Draft);
        if (request.Status is { } status && Enum.TryParse<ApplicationStatus>(status, true, out var parsed))
        {
            query = query.Where(a => a.Status == parsed);
        }

        var limit = Cursor.ClampLimit(request.Limit);
        var skip = 0;
        if (Cursor.Decode(request.Cursor) is { } cursorId)
        {
            // Cursor = last id of the previous page; the page order is score-based, so the cursor marks a position by id in that order.
            var ordered = await query.OrderByDescending(a => a.TotalScore).ThenBy(a => a.SubmittedAt).ThenBy(a => a.Id).Select(a => a.Id).ToListAsync(cancellationToken);
            skip = ordered.IndexOf(cursorId) + 1;
        }

        var items = await query.OrderByDescending(a => a.TotalScore).ThenBy(a => a.SubmittedAt).ThenBy(a => a.Id).Skip(skip).Take(limit + 1).ToListAsync(cancellationToken);
        var page = items.Take(limit).Select(a =>
        {
            JsonElement answers;
            if (request.Blind)
            {
                answers = AnswerFields.Blind(a.AnswersJson);
            }
            else
            {
                using var doc = JsonDocument.Parse(a.AnswersJson);
                answers = doc.RootElement.Clone();
            }

            return new ReviewQueueItem(a.Id, request.Blind ? null : a.LearnerId, a.Status.ToString().ToLowerInvariant(), a.SubmittedAt, a.IsFlagged, a.FlagReason, a.AssessmentScore, a.AssessmentPassed, a.RubricScore, a.TotalScore, a.SeatId, a.OfferExpiresAt, answers);
        }).ToList();
        var shortlistApproved = await db.ShortlistApprovals.AsNoTracking().AnyAsync(s => s.ProgrammeId == programme.ProgrammeId, cancellationToken);
        return new ReviewQueueView(programme.ProgrammeId, programme.SeatCount, programme.SeatsOpen, shortlistApproved,
            counts.ToDictionary(c => c.Key.ToString().ToLowerInvariant(), c => c.Count), new Page<ReviewQueueItem>(page, items.Count > limit ? Cursor.Encode(page[^1].ApplicationId) : null));
    }
}

internal static class ListReviewQueueEndpoint
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapGet("/programmes/{programmeId:guid}/queue", async (Guid programmeId, string? status, bool? blind, string? cursor, int? limit, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListReviewQueueQuery(programmeId, status, blind ?? false, cursor, limit), ct)).ToHttpResult())
            .WithName("ListReviewQueue")
            .WithSummary("Staff: review queue ordered by score; ?blind=true hides identity fields; ?status= filters.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
