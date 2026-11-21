using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;

namespace Plastoids.Modules.Lms.Services;

internal sealed class CohortMembers(LmsDbContext db, IUnitOfWork unitOfWork) : ICohortMembers
{
    public async Task<CohortInfo?> GetAsync(Guid cohortId, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        var cohort = await db.Cohorts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cohortId, cancellationToken);
        if (cohort is null)
        {
            return null;
        }

        var learners = await db.Enrolments.AsNoTracking().Where(e => e.CohortId == cohortId && e.Status == EnrolmentStatus.Active).Select(e => e.LearnerId).Distinct().ToListAsync(cancellationToken);
        return new CohortInfo(cohort.Id, cohort.CourseId, cohort.TutorUserId, cohort.Name, learners);
    }
}
