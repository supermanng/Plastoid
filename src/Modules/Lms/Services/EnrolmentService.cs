using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Lms.Services;

/// <summary>Creates enrolments from payments or accepted offers. Idempotent on the source key; a learner holds one enrolment per course.</summary>
internal sealed class EnrolmentService(LmsDbContext db, ICourseOutlineReader courses, IOutbox outbox, ICurrentTenant tenant, IClock clock)
{
    public static readonly TimeSpan DefaultAccess = TimeSpan.FromDays(365);

    /// <summary>
    /// fundingLabel is shown to the learner as who paid (ADR 0021: "Sponsored by …"); reservedSeat means the seat was reserved
    /// beforehand by a seat block, so the cohort converts the reservation instead of admitting against capacity.
    /// </summary>
    public async Task<Result<Enrolment>> EnrolAsync(Guid learnerId, Guid offeringId, string fundingSource, Guid? fundingSourceId, string sourceKey, Guid? cohortId, string? fundingLabel, bool reservedSeat, CancellationToken ct)
    {
        var existing = await db.Enrolments.SingleOrDefaultAsync(e => e.SourceKey == sourceKey, ct);
        if (existing is not null)
        {
            return existing;
        }

        var courseId = await courses.FindCourseForOfferingAsync(offeringId, ct);
        if (courseId is null)
        {
            return Error.NotFound("offering_not_found", "No course offers that offering.");
        }

        var duplicate = await db.Enrolments.SingleOrDefaultAsync(e => e.LearnerId == learnerId && e.CourseId == courseId && e.Status == EnrolmentStatus.Active, ct);
        if (duplicate is not null)
        {
            return duplicate; // already studying this course: do not double-enrol
        }

        var course = await courses.GetPublishedAsync(courseId.Value, null, ct);
        if (course is null)
        {
            return Error.Conflict("course_not_published", "The course has no published version.");
        }

        var offering = course.Offerings.SingleOrDefault(o => o.OfferingId == offeringId);
        var now = clock.UtcNow;
        var expires = offering?.DurationWeeks is { } weeks ? now.AddDays(7 * weeks + 30) : now.Add(DefaultAccess);

        Cohort? cohort = null;
        if (cohortId is { } cid)
        {
            cohort = await db.Cohorts.SingleOrDefaultAsync(c => c.Id == cid, ct);
        }
        else if (offering?.Format == "cohort")
        {
            cohort = await db.Cohorts.Where(c => c.OfferingId == offeringId && c.Enrolled + c.Reserved < c.Capacity && c.EndsAt > now).OrderBy(c => c.StartsAt).FirstOrDefaultAsync(ct);
        }

        if (cohort is not null)
        {
            if (reservedSeat && cohortId is not null)
            {
                cohort.AdmitReserved();
            }
            else
            {
                var admitted = cohort.Admit();
                if (admitted.IsFailure)
                {
                    return Result.Failure<Enrolment>(admitted.Error);
                }
            }

            expires = cohort.EndsAt.AddDays(30);
        }

        var required = course.Lessons.Count(l => l.Type != "live");
        var enrolment = Enrolment.Create(tenant.TenantId, learnerId, course.CourseId, offeringId, cohort?.Id, course.VersionNo, fundingSource, fundingSourceId, sourceKey, required, now, expires, fundingLabel);
        db.Enrolments.Add(enrolment);
        outbox.Enqueue(new EnrolmentCreated(enrolment.Id, enrolment.Id, learnerId, course.CourseId, offeringId, fundingSource, fundingSourceId, cohort?.Id));
        outbox.Enqueue(NotificationRequested.To(learnerId, "lms.enrolment.created", new Dictionary<string, string> { ["enrolmentId"] = enrolment.Id.ToString("D"), ["courseTitle"] = course.Title, ["courseSlug"] = course.Slug }));
        return enrolment;
    }
}
