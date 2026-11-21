using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Modules.Applications.Contracts;
using Plastoids.Modules.Applications.Domain;
using Plastoids.Modules.Applications.Persistence;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Applications.Services;

/// <summary>Offers, expiry and waitlist backfill (story F4): a seat is reserved when the offer goes out and returned when it lapses.</summary>
public sealed class OfferService(ApplicationsDbContext db, IProgrammeReader programmes, IProgrammeSeats seats, IOutbox outbox, IClock clock)
{
    public const string SystemReason = "Automatic: seat available, next on the waitlist by score";

    public async Task<Result> SendOfferAsync(Application application, ProgrammeSnapshot programme, DateTime now, CancellationToken ct)
    {
        var seat = await seats.ReserveAsync(programme.ProgrammeId, application.Id, application.LearnerId, ct);
        if (seat.IsFailure)
        {
            return Result.Failure(seat.Error);
        }

        var sent = application.SendOffer(seat.Value, now);
        if (sent.IsFailure)
        {
            return sent;
        }

        outbox.Enqueue(new OfferSent(application.Id, application.Id, application.ProgrammeId, application.LearnerId, seat.Value, application.OfferExpiresAt!.Value));
        outbox.Enqueue(NotificationRequested.To(application.LearnerId, "applications.offer.sent", new Dictionary<string, string>
        {
            ["applicationId"] = application.Id.ToString("D"),
            ["programme"] = programme.Name,
            ["sponsor"] = programme.SponsorName,
            ["expiresAt"] = application.OfferExpiresAt.Value.ToString("O", CultureInfo.InvariantCulture),
            ["coPay"] = programme.CoPayPerSeat.ToString(),
        }));
        return Result.Success();
    }

    public void EmitAccepted(Application application, ProgrammeSnapshot programme)
    {
        outbox.Enqueue(new OfferAccepted(application.Id, application.Id, application.ProgrammeId, application.LearnerId, programme.OfferingId, application.SeatId!.Value));
        outbox.Enqueue(NotificationRequested.To(application.LearnerId, "applications.offer.accepted", new Dictionary<string, string> { ["applicationId"] = application.Id.ToString("D"), ["programme"] = programme.Name }));
    }

    /// <summary>Expired offers and unpaid co-pays give their seats back.</summary>
    public async Task<int> ExpireAsync(DateTime now, CancellationToken ct)
    {
        var open = await db.Applications.Where(a => a.Status == ApplicationStatus.Offered || a.Status == ApplicationStatus.PendingCoPay).ToListAsync(ct);
        var expired = 0;
        foreach (var application in open)
        {
            if (!application.ExpireIfDue(now))
            {
                continue;
            }

            expired++;
            if (application.SeatId is { } seatId)
            {
                await seats.ReleaseReservationAsync(seatId, "offer_expired", ct);
            }

            outbox.Enqueue(NotificationRequested.To(application.LearnerId, "applications.offer.expired", new Dictionary<string, string> { ["applicationId"] = application.Id.ToString("D") }));
        }

        return expired;
    }

    /// <summary>Fills open seats from the waitlist in score order, once the shortlist was approved and offers have gone out.</summary>
    public async Task<int> BackfillAsync(Guid programmeId, DateTime now, CancellationToken ct)
    {
        if (!await db.ShortlistApprovals.AnyAsync(s => s.ProgrammeId == programmeId, ct))
        {
            return 0;
        }

        var programme = await programmes.GetAsync(programmeId, ct);
        if (programme is null || programme.Status != "published")
        {
            return 0;
        }

        var open = await seats.CountOpenAsync(programmeId, ct);
        if (open <= 0)
        {
            return 0;
        }

        var waitlist = await db.Applications.Where(a => a.ProgrammeId == programmeId && a.Status == ApplicationStatus.Waitlisted)
            .OrderByDescending(a => a.TotalScore).ThenBy(a => a.SubmittedAt).Take(open).ToListAsync(ct);
        var sent = 0;
        foreach (var application in waitlist)
        {
            var decided = application.Decide(DecisionKinds.Accepted, now);
            if (decided.IsFailure)
            {
                continue;
            }

            db.Decisions.Add(Decision.Record(application.TenantId, application, DecisionKinds.Accepted, SystemReason, Guid.Empty, "backfill", now));
            outbox.Enqueue(new DecisionRecorded(application.Id, application.Id, application.ProgrammeId, application.LearnerId, DecisionKinds.Accepted, Guid.Empty));
            var offer = await SendOfferAsync(application, programme, now, ct);
            if (offer.IsFailure)
            {
                break;
            }

            sent++;
        }

        return sent;
    }

    public DateTime Now => clock.UtcNow;
}

/// <summary>Loads an application the caller owns.</summary>
internal sealed class ApplicationAccess(ApplicationsDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<Application>> MineAsync(Guid applicationId, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var application = await db.Applications.SingleOrDefaultAsync(a => a.Id == applicationId, ct);
        return application is null || application.LearnerId != userId ? Error.NotFound("application_not_found", "No such application.") : application;
    }
}

/// <summary>Parses the programme's rubric JSON: { assessmentWeight, criteria: [{ key, weight, max }] }.</summary>
internal static class RubricParser
{
    public static RubricDefinition Parse(JsonElement? rubric)
    {
        if (rubric is not { ValueKind: JsonValueKind.Object } r)
        {
            return RubricDefinition.AssessmentOnly;
        }

        var weight = r.TryGetProperty("assessmentWeight", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetDecimal() : 50m;
        var criteria = new List<RubricCriterion>();
        if (r.TryGetProperty("criteria", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in list.EnumerateArray())
            {
                var key = c.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString()! : null;
                if (key is null)
                {
                    continue;
                }

                var cw = c.TryGetProperty("weight", out var cwv) && cwv.ValueKind == JsonValueKind.Number ? cwv.GetDecimal() : 1m;
                var max = c.TryGetProperty("max", out var mv) && mv.ValueKind == JsonValueKind.Number ? mv.GetDecimal() : 5m;
                criteria.Add(new RubricCriterion(key, cw, max));
            }
        }

        return criteria.Count == 0 ? RubricDefinition.AssessmentOnly : new RubricDefinition(weight, criteria);
    }
}
