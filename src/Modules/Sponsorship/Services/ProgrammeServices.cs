using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Services;

/// <summary>Loads a programme for a sponsor route (organisation must match) or for staff.</summary>
internal sealed class ProgrammeAccess(SponsorshipDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<Programme>> ForOrganisationAsync(Guid organisationId, Guid programmeId, CancellationToken ct)
    {
        var programme = await db.Programmes.SingleOrDefaultAsync(p => p.Id == programmeId, ct);
        if (programme is null || (programme.OrganisationId != organisationId && !currentUser.Roles.Contains("staff")))
        {
            return Error.NotFound("programme_not_found", "No such programme for this organisation.");
        }

        return programme;
    }

    public async Task<Result<Programme>> ForStaffAsync(Guid programmeId, CancellationToken ct)
    {
        var programme = await db.Programmes.SingleOrDefaultAsync(p => p.Id == programmeId, ct);
        return programme is null ? Error.NotFound("programme_not_found", "No such programme.") : programme;
    }
}

/// <summary>Implements the Sponsorship contracts for Applications; enlists this context in the caller's unit of work.</summary>
internal sealed class ProgrammeReader(SponsorshipDbContext db, IUnitOfWork unitOfWork, IUserDirectory users, IClock clock) : IProgrammeReader, IProgrammeSeats, IEligibilityChecker
{
    public async Task<ProgrammeSnapshot?> GetAsync(Guid programmeId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var p = await db.Programmes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == programmeId, cancellationToken);
        if (p is null)
        {
            return null;
        }

        var open = await db.Seats.AsNoTracking().CountAsync(s => s.ProgrammeId == programmeId && s.Status == SeatStatus.Open, cancellationToken);
        return Snapshot(p, open, clock.UtcNow);
    }

    internal static ProgrammeSnapshot Snapshot(Programme p, int seatsOpen, DateTime now) => new(
        p.Id, p.OrganisationId, p.SponsorName, p.Name, p.CourseId ?? Guid.Empty, p.OfferingId ?? Guid.Empty, p.Status.ToString().ToLowerInvariant(), p.SelectionMode,
        p.SeatCount, seatsOpen, p.IsAcceptingApplications(now), p.ApplicationsCloseAt, p.StartsAt ?? DateTime.MinValue, p.EndsAt ?? DateTime.MinValue,
        p.SponsorSharePercent, p.TuitionPerSeat, p.CoPayPerSeat, Parse(p.EligibilityRulesJson) ?? JsonDocument.Parse("{}").RootElement.Clone(), Parse(p.EntryAssessmentJson), Parse(p.ScoringRubricJson));

    private static JsonElement? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public async Task<Result<Guid>> ReserveAsync(Guid programmeId, Guid applicationId, Guid learnerId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var existing = await db.Seats.SingleOrDefaultAsync(s => s.ProgrammeId == programmeId && s.ApplicationId == applicationId && s.Status != SeatStatus.Open, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        // Several offers are sent in one transaction: read the candidates, then trust the tracked (in-memory) status so a seat reserved moments ago is skipped.
        var candidates = await db.Seats.Where(s => s.ProgrammeId == programmeId).OrderBy(s => s.SeatNo).ToListAsync(cancellationToken);
        var seat = candidates.FirstOrDefault(s => s.Status == SeatStatus.Open);
        if (seat is null)
        {
            return Error.Conflict("no_seats_available", "Every funded seat is reserved or filled.");
        }

        var reserved = seat.Reserve(applicationId, learnerId, clock.UtcNow);
        return reserved.IsFailure ? Result.Failure<Guid>(reserved.Error) : seat.Id;
    }

    public async Task<Result> ReleaseReservationAsync(Guid seatId, string reason, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var seat = await db.Seats.SingleOrDefaultAsync(s => s.Id == seatId, cancellationToken);
        if (seat is null)
        {
            return Error.NotFound("seat_not_found", "No such seat.");
        }

        return seat.Status == SeatStatus.Open ? Result.Success() : seat.Release(reason, clock.UtcNow);
    }

    public async Task<int> CountOpenAsync(Guid programmeId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        // Tracked state wins over the database row: a seat released earlier in this transaction already counts as open.
        var seats = await db.Seats.Where(s => s.ProgrammeId == programmeId).ToListAsync(cancellationToken);
        return seats.Count(s => s.Status == SeatStatus.Open);
    }

    public async Task<EligibilityOutcome> CheckAsync(Guid programmeId, Guid learnerId, ApplicantFacts facts, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var rulesJson = await db.Programmes.AsNoTracking().Where(p => p.Id == programmeId).Select(p => p.EligibilityRulesJson).SingleOrDefaultAsync(cancellationToken);
        if (rulesJson is null)
        {
            return new EligibilityOutcome(false, ["programme_not_found"]);
        }

        var user = await users.GetAsync(learnerId, cancellationToken);
        var previouslySponsored = await db.Seats.AsNoTracking().AnyAsync(s => s.LearnerId == learnerId && s.Status == SeatStatus.Filled && s.ProgrammeId != programmeId, cancellationToken);
        using var rules = JsonDocument.Parse(rulesJson);
        return EligibilityEngine.Evaluate(rules.RootElement, user?.AgeYears, facts, previouslySponsored);
    }

    private async Task EnlistAsync(CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }
    }
}
