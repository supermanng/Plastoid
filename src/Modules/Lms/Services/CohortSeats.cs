using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Lms.Services;

/// <summary>ICohortSeats for Sponsorship (ADR 0021): reserve and release cohort capacity inside the caller's unit of work.</summary>
internal sealed class CohortSeats(LmsDbContext db, IUnitOfWork unitOfWork, IClock clock) : ICohortSeats
{
    public async Task<CohortSeatInfo?> GetAsync(Guid cohortId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var c = await db.Cohorts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == cohortId, cancellationToken);
        return c is null ? null : new CohortSeatInfo(c.Id, c.CourseId, c.OfferingId, c.TutorUserId, c.Name, c.StartsAt, c.EndsAt, c.Capacity, c.Enrolled, c.Reserved);
    }

    public async Task<Result> ReserveAsync(Guid cohortId, int seats, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var cohort = await db.Cohorts.SingleOrDefaultAsync(x => x.Id == cohortId, cancellationToken);
        if (cohort is null)
        {
            return Error.NotFound("cohort_not_found", "No cohort with that id.");
        }

        var reserved = cohort.Reserve(seats, clock.UtcNow);
        if (reserved.IsFailure)
        {
            return reserved;
        }

        await SaveIfStandaloneAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ReleaseAsync(Guid cohortId, int seats, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var cohort = await db.Cohorts.SingleOrDefaultAsync(x => x.Id == cohortId, cancellationToken);
        if (cohort is null)
        {
            return Error.NotFound("cohort_not_found", "No cohort with that id.");
        }

        cohort.ReleaseReserved(seats);
        await SaveIfStandaloneAsync(cancellationToken);
        return Result.Success();
    }

    private async Task EnlistAsync(CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }
    }

    /// <summary>Outside a unit of work (a job, say) the change is saved here; inside one the owner's commit saves every enlisted context.</summary>
    private async Task SaveIfStandaloneAsync(CancellationToken cancellationToken)
    {
        if (!unitOfWork.IsActive)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
