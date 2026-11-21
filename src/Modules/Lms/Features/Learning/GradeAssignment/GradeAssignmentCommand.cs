using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.Modules.Lms.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Lms.Features.Learning.GradeAssignment;

public sealed record RubricScore(string Criterion, decimal Points, decimal MaxPoints, string? Comment);

/// <summary>Tutor (course owner) grades with a rubric; passing marks the assignment lesson complete.</summary>
public sealed record GradeAssignmentCommand(Guid SubmissionId, IReadOnlyList<RubricScore> Rubric, string? Feedback) : ICommand<GradeAssignmentResponse>;

public sealed record GradeAssignmentRequest(IReadOnlyList<RubricScore> Rubric, string? Feedback);

public sealed record GradeAssignmentResponse(Guid SubmissionId, decimal Score, decimal MaxScore, decimal ScorePercent, bool Passed, bool EnrolmentCompleted);

internal sealed class GradeAssignmentValidator : AbstractValidator<GradeAssignmentCommand>
{
    public GradeAssignmentValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.Rubric).NotEmpty().Must(r => r.Count <= 30);
        RuleForEach(x => x.Rubric).ChildRules(r =>
        {
            r.RuleFor(x => x.Criterion).NotEmpty().MaximumLength(120);
            r.RuleFor(x => x.MaxPoints).GreaterThan(0);
            r.RuleFor(x => x).Must(x => x.Points >= 0 && x.Points <= x.MaxPoints).WithMessage("points must be between 0 and maxPoints.");
        });
        RuleFor(x => x.Feedback).MaximumLength(2000);
    }
}

internal sealed class GradeAssignmentHandler(LmsDbContext db, ProgressService progress, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<GradeAssignmentCommand, GradeAssignmentResponse>
{
    public async Task<Result<GradeAssignmentResponse>> Handle(GradeAssignmentCommand request, CancellationToken cancellationToken)
    {
        var submission = await db.Submissions.SingleOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);
        if (submission is null)
        {
            return Error.NotFound("submission_not_found", "No such submission.");
        }

        var enrolment = await db.Enrolments.SingleAsync(e => e.Id == submission.EnrolmentId, cancellationToken);
        var course = await progress.CourseForAsync(enrolment, cancellationToken);
        if (course.IsFailure)
        {
            return Result.Failure<GradeAssignmentResponse>(course.Error);
        }

        var graderId = currentUser.UserId ?? Guid.Empty;
        if (course.Value.OwnerUserId != graderId && !currentUser.Roles.Contains("staff"))
        {
            return Error.Forbidden("not_course_tutor", "Only the course tutor (or staff) can grade.");
        }

        var lesson = course.Value.Lessons.Single(l => l.LessonId == submission.LessonId);
        var score = request.Rubric.Sum(r => r.Points);
        var max = request.Rubric.Sum(r => r.MaxPoints);
        var graded = submission.Grade(score, max, ProgressService.PassMarkOf(lesson), JsonSerializer.Serialize(request.Rubric), request.Feedback, graderId, clock.UtcNow);
        if (graded.IsFailure)
        {
            return Result.Failure<GradeAssignmentResponse>(graded.Error);
        }

        var completedEnrolment = false;
        if (submission.Passed)
        {
            var row = await db.Progress.SingleOrDefaultAsync(p => p.EnrolmentId == enrolment.Id && p.LessonId == submission.LessonId, cancellationToken);
            if (row is null)
            {
                row = LessonProgress.Start(enrolment.TenantId, enrolment.Id, submission.LessonId, clock.UtcNow);
                db.Progress.Add(row);
            }

            row.MarkCompleted(clock.UtcNow);
            var completed = await progress.RecomputeAsync(enrolment, cancellationToken);
            if (completed.IsFailure)
            {
                return Result.Failure<GradeAssignmentResponse>(completed.Error);
            }

            completedEnrolment = completed.Value;
        }

        outbox.Enqueue(NotificationRequested.To(enrolment.LearnerId, "lms.assignment.graded", new Dictionary<string, string> { ["enrolmentId"] = enrolment.Id.ToString("D"), ["lessonTitle"] = lesson.Title, ["scorePercent"] = (submission.ScorePercent ?? 0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) }));
        return new GradeAssignmentResponse(submission.Id, score, max, submission.ScorePercent ?? 0, submission.Passed, completedEnrolment);
    }
}

internal static class GradeAssignmentEndpoint
{
    public static void Map(IEndpointRouteBuilder lms)
    {
        lms.MapPost("/submissions/{submissionId:guid}/grade", async (Guid submissionId, GradeAssignmentRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GradeAssignmentCommand(submissionId, body.Rubric, body.Feedback), ct)).ToHttpResult())
            .WithName("GradeAssignment")
            .WithSummary("Tutor: grade a submission with a rubric; pass mark from the lesson content (default 70 %).")
            .RequireAuthorization(Policies.Tutor);
    }
}
