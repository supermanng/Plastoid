using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.Modules.Lms.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Lms.Features.Learning.QuizAttempts;

public sealed record StartAttemptCommand(Guid EnrolmentId, Guid LessonId) : ICommand<AttemptView>;

public sealed record SaveAnswersCommand(Guid AttemptId, JsonElement Answers) : ICommand<AttemptView>;

public sealed record SubmitAttemptCommand(Guid AttemptId, JsonElement? Answers) : ICommand<AttemptResultView>;

public sealed record AttemptView(Guid AttemptId, Guid EnrolmentId, Guid LessonId, int Number, string Status, DateTime StartedAt, DateTime DeadlineAt, int SecondsRemaining);

public sealed record AttemptResultView(Guid AttemptId, string Status, decimal Score, decimal MaxScore, bool Passed, int Correct, int Total, bool EnrolmentCompleted);

internal sealed class StartAttemptValidator : AbstractValidator<StartAttemptCommand>
{
    public StartAttemptValidator()
    {
        RuleFor(x => x.EnrolmentId).NotEmpty();
        RuleFor(x => x.LessonId).NotEmpty();
    }
}

internal sealed class SaveAnswersValidator : AbstractValidator<SaveAnswersCommand>
{
    public SaveAnswersValidator()
    {
        RuleFor(x => x.AttemptId).NotEmpty();
        RuleFor(x => x.Answers).Must(a => a.ValueKind == JsonValueKind.Object).WithMessage("answers must be an object keyed by question index.");
    }
}

internal sealed class SubmitAttemptValidator : AbstractValidator<SubmitAttemptCommand>
{
    public SubmitAttemptValidator()
    {
        RuleFor(x => x.AttemptId).NotEmpty();
    }
}

internal sealed class StartAttemptHandler(LmsDbContext db, LmsAccess access, ProgressService progress, IClock clock) : ICommandHandler<StartAttemptCommand, AttemptView>
{
    public async Task<Result<AttemptView>> Handle(StartAttemptCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MyEnrolmentAsync(request.EnrolmentId, cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<AttemptView>(mine.Error);
        }

        var course = await progress.CourseForAsync(mine.Value, cancellationToken);
        if (course.IsFailure)
        {
            return Result.Failure<AttemptView>(course.Error);
        }

        var lesson = course.Value.Lessons.SingleOrDefault(l => l.LessonId == request.LessonId && l.Type == "quiz");
        if (lesson is null)
        {
            return Error.NotFound("quiz_not_found", "No quiz with that lesson id in this course.");
        }

        var open = await db.Attempts.SingleOrDefaultAsync(a => a.EnrolmentId == mine.Value.Id && a.LessonId == lesson.LessonId && a.Status == AttemptStatus.InProgress, cancellationToken);
        var now = clock.UtcNow;
        if (open is not null && now <= open.DeadlineAt)
        {
            return View(open, now);
        }

        var number = await db.Attempts.CountAsync(a => a.EnrolmentId == mine.Value.Id && a.LessonId == lesson.LessonId, cancellationToken) + 1;
        var attempt = Attempt.Start(mine.Value.TenantId, mine.Value.Id, lesson.LessonId, number, ProgressService.TimeLimitOf(lesson), now);
        db.Attempts.Add(attempt);
        return View(attempt, now);
    }

    internal static AttemptView View(Attempt a, DateTime now) => new(a.Id, a.EnrolmentId, a.LessonId, a.Number, a.Status.ToString().ToLowerInvariant(), a.StartedAt, a.DeadlineAt, (int)Math.Max(0, (a.DeadlineAt - now).TotalSeconds));
}

internal sealed class SaveAnswersHandler(LmsDbContext db, ICurrentUser currentUser, IClock clock) : ICommandHandler<SaveAnswersCommand, AttemptView>
{
    public async Task<Result<AttemptView>> Handle(SaveAnswersCommand request, CancellationToken cancellationToken)
    {
        var attempt = await db.Attempts.SingleOrDefaultAsync(a => a.Id == request.AttemptId, cancellationToken);
        if (attempt is null || !await db.Enrolments.AnyAsync(e => e.Id == attempt.EnrolmentId && e.LearnerId == currentUser.UserId, cancellationToken))
        {
            return Error.NotFound("attempt_not_found", "No such attempt.");
        }

        var saved = attempt.SaveAnswers(request.Answers.GetRawText(), clock.UtcNow);
        return saved.IsFailure ? Result.Failure<AttemptView>(saved.Error) : StartAttemptHandler.View(attempt, clock.UtcNow);
    }
}

internal sealed class SubmitAttemptHandler(LmsDbContext db, LmsAccess access, ProgressService progress, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<SubmitAttemptCommand, AttemptResultView>
{
    public async Task<Result<AttemptResultView>> Handle(SubmitAttemptCommand request, CancellationToken cancellationToken)
    {
        var attempt = await db.Attempts.SingleOrDefaultAsync(a => a.Id == request.AttemptId, cancellationToken);
        if (attempt is null || !await db.Enrolments.AnyAsync(e => e.Id == attempt.EnrolmentId && e.LearnerId == currentUser.UserId, cancellationToken))
        {
            return Error.NotFound("attempt_not_found", "No such attempt.");
        }

        var now = clock.UtcNow;
        if (request.Answers is { } latest && latest.ValueKind == JsonValueKind.Object)
        {
            var saved = attempt.SaveAnswers(latest.GetRawText(), now);
            if (saved.IsFailure)
            {
                return Result.Failure<AttemptResultView>(saved.Error);
            }
        }

        var mine = await access.MyEnrolmentAsync(attempt.EnrolmentId, cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<AttemptResultView>(mine.Error);
        }

        var course = await progress.CourseForAsync(mine.Value, cancellationToken);
        if (course.IsFailure)
        {
            return Result.Failure<AttemptResultView>(course.Error);
        }

        var lesson = course.Value.Lessons.Single(l => l.LessonId == attempt.LessonId);
        using var answers = JsonDocument.Parse(attempt.AnswersJson);
        var grade = Grader.Grade(lesson.Content ?? default, answers.RootElement);
        var submitted = attempt.Submit(grade, now);
        if (submitted.IsFailure)
        {
            return Result.Failure<AttemptResultView>(submitted.Error);
        }

        outbox.Enqueue(new AttemptGraded(mine.Value.Id, attempt.Id, mine.Value.Id, attempt.LessonId, attempt.Score, attempt.MaxScore, attempt.Passed));
        var completedEnrolment = false;
        if (attempt.Passed)
        {
            var row = await db.Progress.SingleOrDefaultAsync(p => p.EnrolmentId == mine.Value.Id && p.LessonId == attempt.LessonId, cancellationToken);
            if (row is null)
            {
                row = LessonProgress.Start(mine.Value.TenantId, mine.Value.Id, attempt.LessonId, now);
                db.Progress.Add(row);
            }

            row.MarkCompleted(now);
            var completed = await progress.RecomputeAsync(mine.Value, cancellationToken);
            if (completed.IsFailure)
            {
                return Result.Failure<AttemptResultView>(completed.Error);
            }

            completedEnrolment = completed.Value;
        }

        return new AttemptResultView(attempt.Id, attempt.Status.ToString().ToLowerInvariant(), attempt.Score, attempt.MaxScore, attempt.Passed, grade.Correct, grade.Total, completedEnrolment);
    }
}

public sealed record SaveAnswersRequest(JsonElement Answers);

public sealed record SubmitAttemptRequest(JsonElement? Answers);

internal static class QuizAttemptEndpoints
{
    public static void Map(IEndpointRouteBuilder lms)
    {
        lms.MapPost("/enrolments/{enrolmentId:guid}/lessons/{lessonId:guid}/attempts", async (Guid enrolmentId, Guid lessonId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new StartAttemptCommand(enrolmentId, lessonId), ct)).ToHttpResult(StatusCodes.Status201Created))
            .WithName("StartAttempt")
            .WithSummary("Start (or resume) a timed quiz attempt; the deadline is enforced server-side.")
            .RequireAuthorization(Policies.Authenticated);

        lms.MapPut("/attempts/{attemptId:guid}/answers", async (Guid attemptId, SaveAnswersRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SaveAnswersCommand(attemptId, body.Answers), ct)).ToHttpResult())
            .WithName("SaveAnswers")
            .WithSummary("Save answers { \"0\": 1, \"1\": [0,2], \"2\": \"text\" } before the deadline.")
            .RequireAuthorization(Policies.Authenticated);

        lms.MapPost("/attempts/{attemptId:guid}/submit", async (Guid attemptId, SubmitAttemptRequest? body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SubmitAttemptCommand(attemptId, body?.Answers), ct)).ToHttpResult())
            .WithName("SubmitAttempt")
            .WithSummary("Submit and auto-grade (MCQ, multi-select, short answer). Late submissions are expired.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
