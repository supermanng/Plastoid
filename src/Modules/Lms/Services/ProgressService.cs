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

/// <summary>Recomputes progress_pct from lesson progress and applies the completion rule (all required lessons + every pass mark).</summary>
internal sealed class ProgressService(LmsDbContext db, ICourseOutlineReader courses, IOutbox outbox, IClock clock)
{
    public async Task<Result<PublishedCourse>> CourseForAsync(Enrolment enrolment, CancellationToken ct)
    {
        var course = await courses.GetPublishedAsync(enrolment.CourseId, enrolment.VersionNo, ct);
        return course is null ? Error.NotFound("course_version_missing", "The course version of this enrolment no longer exists.") : course;
    }

    public async Task<Result<bool>> RecomputeAsync(Enrolment enrolment, CancellationToken ct)
    {
        var courseResult = await CourseForAsync(enrolment, ct);
        if (courseResult.IsFailure)
        {
            return Result.Failure<bool>(courseResult.Error);
        }

        var course = courseResult.Value;
        var required = course.Lessons.Where(l => l.Type != "live").Select(l => l.LessonId).ToHashSet();
        var completed = await db.Progress.Where(p => p.EnrolmentId == enrolment.Id && p.Status == LessonStatus.Completed).Select(p => p.LessonId).ToListAsync(ct);
        var completedLocal = db.Progress.Local.Where(p => p.EnrolmentId == enrolment.Id && p.Status == LessonStatus.Completed).Select(p => p.LessonId);
        var completedRequired = completed.Concat(completedLocal).Distinct().Count(required.Contains);
        enrolment.UpdateProgress(completedRequired);

        // Pass marks: every quiz needs a passed attempt; every assignment a passed grade.
        var quizIds = course.Lessons.Where(l => l.Type == "quiz").Select(l => l.LessonId).ToList();
        var assignmentIds = course.Lessons.Where(l => l.Type == "assignment").Select(l => l.LessonId).ToList();
        var passedQuizzes = (await db.Attempts.Where(a => a.EnrolmentId == enrolment.Id && a.Passed).Select(a => a.LessonId).ToListAsync(ct)).Concat(db.Attempts.Local.Where(a => a.EnrolmentId == enrolment.Id && a.Passed).Select(a => a.LessonId)).ToHashSet();
        var passedAssignments = (await db.Submissions.Where(s => s.EnrolmentId == enrolment.Id && s.Passed).Select(s => s.LessonId).ToListAsync(ct)).Concat(db.Submissions.Local.Where(s => s.EnrolmentId == enrolment.Id && s.Passed).Select(s => s.LessonId)).ToHashSet();
        var allPassed = quizIds.All(passedQuizzes.Contains) && assignmentIds.All(passedAssignments.Contains);

        var scores = new List<decimal>();
        foreach (var quiz in quizIds)
        {
            var best = (await db.Attempts.Where(a => a.EnrolmentId == enrolment.Id && a.LessonId == quiz && a.Status == AttemptStatus.Submitted).Select(a => (decimal?)a.Score).MaxAsync(ct))
                ?? db.Attempts.Local.Where(a => a.EnrolmentId == enrolment.Id && a.LessonId == quiz && a.Status == AttemptStatus.Submitted).Select(a => (decimal?)a.Score).Max();
            if (best is { } s)
            {
                scores.Add(s);
            }
        }

        foreach (var assignment in assignmentIds)
        {
            var graded = await db.Submissions.Where(s => s.EnrolmentId == enrolment.Id && s.LessonId == assignment && s.Status == SubmissionStatus.Graded).OrderByDescending(s => s.GradedAt).FirstOrDefaultAsync(ct)
                ?? db.Submissions.Local.FirstOrDefault(s => s.EnrolmentId == enrolment.Id && s.LessonId == assignment && s.Status == SubmissionStatus.Graded);
            if (graded?.ScorePercent is { } pct)
            {
                scores.Add(pct);
            }
        }

        var finalScore = scores.Count == 0 ? 100m : Math.Round(scores.Average(), 2);
        if (enrolment.TryComplete(allPassed, finalScore, clock.UtcNow))
        {
            outbox.Enqueue(new EnrolmentCompleted(enrolment.Id, enrolment.Id, enrolment.LearnerId, enrolment.CourseId, enrolment.OfferingId, enrolment.VersionNo, finalScore, enrolment.FundingSource, enrolment.FundingSourceId));
            outbox.Enqueue(NotificationRequested.To(enrolment.LearnerId, "lms.enrolment.completed", new Dictionary<string, string> { ["enrolmentId"] = enrolment.Id.ToString("D"), ["courseTitle"] = course.Title, ["finalScore"] = finalScore.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) }));
            return true;
        }

        return false;
    }

    public static int PassMarkOf(PublishedLesson lesson)
        => lesson.Content is { } c && c.ValueKind == System.Text.Json.JsonValueKind.Object && c.TryGetProperty("passMarkPercent", out var pm) && pm.ValueKind == System.Text.Json.JsonValueKind.Number ? pm.GetInt32() : 70;

    public static int TimeLimitOf(PublishedLesson lesson)
        => lesson.Content is { } c && c.ValueKind == System.Text.Json.JsonValueKind.Object && c.TryGetProperty("timeLimitMinutes", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.Number ? Math.Clamp(t.GetInt32(), 1, 600) : Attempt.DefaultTimeLimitMinutes;
}

/// <summary>Loads an enrolment the caller owns (or any, for staff) with its tenant-scoped checks.</summary>
internal sealed class LmsAccess(LmsDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<Enrolment>> MyEnrolmentAsync(Guid enrolmentId, CancellationToken ct, bool requireActive = true)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var enrolment = await db.Enrolments.SingleOrDefaultAsync(e => e.Id == enrolmentId, ct);
        if (enrolment is null || (enrolment.LearnerId != userId && !currentUser.Roles.Contains("staff")))
        {
            return Error.NotFound("enrolment_not_found", "No such enrolment.");
        }

        if (requireActive && !enrolment.IsActive)
        {
            return Error.Conflict("enrolment_not_active", $"This enrolment is {enrolment.Status.ToString().ToLowerInvariant()}.");
        }

        return enrolment;
    }
}

/// <summary>Catalogue's review gate (IEnrolmentProgress) answered from real enrolments (build prompt Sprint 5).</summary>
internal sealed class LmsEnrolmentProgress(LmsDbContext db, Plastoids.Infrastructure.Persistence.IUnitOfWork unitOfWork) : IEnrolmentProgress
{
    public async Task<EnrolmentProgressSnapshot?> GetAsync(Guid enrolmentId, Guid learnerId, Guid courseId, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        var e = await db.Enrolments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == enrolmentId && x.LearnerId == learnerId && x.CourseId == courseId, cancellationToken);
        return e is null ? null : new EnrolmentProgressSnapshot(e.Id, e.LearnerId, e.CourseId, e.ProgressPct, e.Status != EnrolmentStatus.Withdrawn);
    }
}
