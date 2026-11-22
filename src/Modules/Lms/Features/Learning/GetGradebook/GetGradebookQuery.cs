using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.Modules.Lms.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Lms.Features.Learning.GetGradebook;

/// <summary>Learner view of one enrolment (EnrolmentId) or tutor view of a whole course (CourseId).</summary>
public sealed record GetGradebookQuery(Guid? EnrolmentId, Guid? CourseId) : IQuery<Gradebook>;

public sealed record GradebookItem(Guid LessonId, string Title, string Type, string Status, decimal? BestScore, bool Passed, int Attempts);

public sealed record GradebookRow(Guid EnrolmentId, Guid LearnerId, decimal ProgressPct, string Status, decimal? FinalScore, IReadOnlyList<GradebookItem> Items);

public sealed record Gradebook(Guid CourseId, string CourseTitle, IReadOnlyList<GradebookRow> Rows);

internal sealed class GetGradebookHandler(LmsDbContext db, ICourseOutlineReader courses, TeachingAccess teaching, ICurrentUser currentUser) : IQueryHandler<GetGradebookQuery, Gradebook>
{
    public async Task<Result<Gradebook>> Handle(GetGradebookQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        List<Enrolment> enrolments;
        Guid courseId;
        if (request.EnrolmentId is { } enrolmentId)
        {
            var e = await db.Enrolments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == enrolmentId, cancellationToken);
            if (e is null || (e.LearnerId != userId && !currentUser.Roles.Contains("staff")))
            {
                return Error.NotFound("enrolment_not_found", "No such enrolment.");
            }

            enrolments = [e];
            courseId = e.CourseId;
        }
        else if (request.CourseId is { } cid)
        {
            var course = await courses.GetPublishedAsync(cid, null, cancellationToken);
            if (course is null || !await teaching.CanManageCourseAsync(course, userId, cancellationToken))
            {
                return Error.Forbidden("not_course_tutor", "Only the course's teachers (owner, organisation authors, engaged tutors) or staff can see the course gradebook.");
            }

            enrolments = await db.Enrolments.AsNoTracking().Where(e => e.CourseId == cid).OrderBy(e => e.StartedAt).Take(500).ToListAsync(cancellationToken);
            courseId = cid;
        }
        else
        {
            return Error.Validation("gradebook_scope_required", "Give enrolmentId or courseId.");
        }

        var published = await courses.GetPublishedAsync(courseId, null, cancellationToken);
        if (published is null)
        {
            return Error.NotFound("course_not_found", "No such course.");
        }

        var ids = enrolments.Select(e => e.Id).ToList();
        var progress = await db.Progress.AsNoTracking().Where(p => ids.Contains(p.EnrolmentId)).ToListAsync(cancellationToken);
        var attempts = await db.Attempts.AsNoTracking().Where(a => ids.Contains(a.EnrolmentId)).ToListAsync(cancellationToken);
        var submissions = await db.Submissions.AsNoTracking().Where(s => ids.Contains(s.EnrolmentId)).ToListAsync(cancellationToken);

        var rows = enrolments.Select(e => new GradebookRow(e.Id, e.LearnerId, e.ProgressPct, e.Status.ToString().ToLowerInvariant(), e.FinalScore,
            published.Lessons.Where(l => l.Type != "live").Select(l =>
            {
                var p = progress.FirstOrDefault(x => x.EnrolmentId == e.Id && x.LessonId == l.LessonId);
                var quizAttempts = attempts.Where(a => a.EnrolmentId == e.Id && a.LessonId == l.LessonId).ToList();
                var sub = submissions.Where(s => s.EnrolmentId == e.Id && s.LessonId == l.LessonId).OrderByDescending(s => s.SubmittedAt).FirstOrDefault();
                decimal? best = l.Type == "quiz" ? quizAttempts.Where(a => a.Status == AttemptStatus.Submitted).Select(a => (decimal?)a.Score).Max() : sub?.ScorePercent;
                var passed = l.Type == "quiz" ? quizAttempts.Any(a => a.Passed) : l.Type == "assignment" ? sub?.Passed == true : p?.Status == LessonStatus.Completed;
                return new GradebookItem(l.LessonId, l.Title, l.Type, (p?.Status ?? LessonStatus.NotStarted).ToString().ToLowerInvariant(), best, passed, quizAttempts.Count);
            }).ToList())).ToList();

        return new Gradebook(courseId, published.Title, rows);
    }
}

internal static class GetGradebookEndpoint
{
    public static void Map(IEndpointRouteBuilder lms)
    {
        lms.MapGet("/enrolments/{enrolmentId:guid}/gradebook", async (Guid enrolmentId, ISender sender, CancellationToken ct) => (await sender.Send(new GetGradebookQuery(enrolmentId, null), ct)).ToHttpResult())
            .WithName("GetMyGradebook")
            .RequireAuthorization(Policies.Authenticated);
        lms.MapGet("/courses/{courseId:guid}/gradebook", async (Guid courseId, ISender sender, CancellationToken ct) => (await sender.Send(new GetGradebookQuery(null, courseId), ct)).ToHttpResult())
            .WithName("GetCourseGradebook")
            .WithSummary("Teacher: every learner's progress and scores for a course.")
            .RequireAuthorization(Policies.Mfa); // teaching access is decided in the handler (ADR 0019 §4)
    }
}
