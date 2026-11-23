using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Lms.Contracts;

/// <summary>A cohort as a sponsor sees it (ADR 0021): dates, capacity, seats already enrolled and seats reserved by seat blocks.</summary>
public sealed record CohortSeatInfo(Guid CohortId, Guid CourseId, Guid OfferingId, Guid TutorUserId, string Name, DateTime StartsAt, DateTime EndsAt, int Capacity, int Enrolled, int Reserved)
{
    public int SeatsLeft => Math.Max(0, Capacity - Enrolled - Reserved);
    public bool HasEnded(DateTime utcNow) => EndsAt <= utcNow;
}

/// <summary>In-process cohort capacity for Sponsorship (ADR 0021). Joins the caller's unit of work.</summary>
public interface ICohortSeats
{
    Task<CohortSeatInfo?> GetAsync(Guid cohortId, CancellationToken cancellationToken);

    /// <summary>Holds <paramref name="seats"/> of the cohort's capacity for a seat block; fails with cohort_full or cohort_ended.</summary>
    Task<Result> ReserveAsync(Guid cohortId, int seats, CancellationToken cancellationToken);

    /// <summary>Gives back reserved seats that were never allocated (never below zero).</summary>
    Task<Result> ReleaseAsync(Guid cohortId, int seats, CancellationToken cancellationToken);
}
