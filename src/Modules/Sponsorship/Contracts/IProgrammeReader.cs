using System.Text.Json;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Sponsorship.Contracts;

/// <summary>What Applications needs to know about a programme (never the budget or the sponsor's internal notes).</summary>
public sealed record ProgrammeSnapshot(
    Guid ProgrammeId,
    Guid OrganisationId,
    string SponsorName,
    string Name,
    Guid CourseId,
    Guid OfferingId,
    string Status,
    string SelectionMode,
    int SeatCount,
    int SeatsOpen,
    bool AcceptingApplications,
    DateTime? ApplicationsCloseAt,
    DateTime StartsAt,
    DateTime EndsAt,
    int SponsorSharePercent,
    Money TuitionPerSeat,
    Money CoPayPerSeat,
    JsonElement EligibilityRules,
    JsonElement? EntryAssessment,
    JsonElement? ScoringRubric);

public interface IProgrammeReader
{
    Task<ProgrammeSnapshot?> GetAsync(Guid programmeId, CancellationToken cancellationToken);
}

/// <summary>Seat inventory operations for Applications: an offer reserves a seat; a declined or expired offer gives it back. Joins the caller's unit of work.</summary>
public interface IProgrammeSeats
{
    Task<Result<Guid>> ReserveAsync(Guid programmeId, Guid applicationId, Guid learnerId, CancellationToken cancellationToken);
    Task<Result> ReleaseReservationAsync(Guid seatId, string reason, CancellationToken cancellationToken);
    Task<int> CountOpenAsync(Guid programmeId, CancellationToken cancellationToken);
}

/// <summary>Facts an applicant declares on the form. Protected characteristics are deliberately absent (blueprint §9.13).</summary>
public sealed record ApplicantFacts(string? CountryOfResidence, string? StateOfResidence, string? EducationLevel, string? EmploymentStatus);

public sealed record EligibilityOutcome(bool Eligible, IReadOnlyList<string> Reasons);

/// <summary>Deterministic eligibility evaluation of a learner against a programme's rules (story F2, &lt; 1 s).</summary>
public interface IEligibilityChecker
{
    Task<EligibilityOutcome> CheckAsync(Guid programmeId, Guid learnerId, ApplicantFacts facts, CancellationToken cancellationToken);
}
