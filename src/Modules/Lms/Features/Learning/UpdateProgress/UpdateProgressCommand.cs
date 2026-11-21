using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.Modules.Lms.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Lms.Features.Learning.UpdateProgress;

public sealed record ProgressItem(Guid LessonId, string Status, int PositionSeconds, int Page, decimal PercentComplete, DateTime ClientUpdatedAt);

/// <summary>Batched progress sync (offline clients): newest record per lesson wins, completion never regresses, progress_pct recomputed.</summary>
public sealed record UpdateProgressCommand(Guid EnrolmentId, IReadOnlyList<ProgressItem> Items) : ICommand<UpdateProgressResponse>;

public sealed record UpdateProgressRequest(IReadOnlyList<ProgressItem> Items);

public sealed record UpdateProgressResponse(Guid EnrolmentId, int Applied, int Ignored, decimal ProgressPct, int CompletedLessons, int RequiredLessons, bool Completed);

internal sealed class UpdateProgressValidator : AbstractValidator<UpdateProgressCommand>
{
    public UpdateProgressValidator()
    {
        RuleFor(x => x.EnrolmentId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= 500);
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.LessonId).NotEmpty();
            i.RuleFor(x => x.Status).Must(s => s is "notstarted" or "inprogress" or "completed").WithMessage("status must be notstarted, inprogress or completed.");
            i.RuleFor(x => x.PercentComplete).InclusiveBetween(0, 100);
        });
    }
}

internal sealed class UpdateProgressHandler(LmsDbContext db, LmsAccess access, ProgressService progress, IClock clock) : ICommandHandler<UpdateProgressCommand, UpdateProgressResponse>
{
    public async Task<Result<UpdateProgressResponse>> Handle(UpdateProgressCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MyEnrolmentAsync(request.EnrolmentId, cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<UpdateProgressResponse>(mine.Error);
        }

        var enrolment = mine.Value;
        var course = await progress.CourseForAsync(enrolment, cancellationToken);
        if (course.IsFailure)
        {
            return Result.Failure<UpdateProgressResponse>(course.Error);
        }

        var known = course.Value.Lessons.ToDictionary(l => l.LessonId);
        var existing = await db.Progress.Where(p => p.EnrolmentId == enrolment.Id).ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        int applied = 0, ignored = 0;
        foreach (var item in request.Items)
        {
            if (!known.TryGetValue(item.LessonId, out var lesson) || lesson.Type is "quiz" or "assignment")
            {
                ignored++; // quizzes and assignments complete through grading, not self-report
                continue;
            }

            var row = existing.SingleOrDefault(p => p.LessonId == item.LessonId);
            if (row is null)
            {
                row = LessonProgress.Start(enrolment.TenantId, enrolment.Id, item.LessonId, now);
                db.Progress.Add(row);
                existing.Add(row);
            }

            var status = Enum.Parse<LessonStatus>(item.Status, true);
            if (row.Merge(status, item.PositionSeconds, item.Page, item.PercentComplete, DateTime.SpecifyKind(item.ClientUpdatedAt, DateTimeKind.Utc), now))
            {
                applied++;
            }
            else
            {
                ignored++;
            }
        }

        var completed = await progress.RecomputeAsync(enrolment, cancellationToken);
        if (completed.IsFailure)
        {
            return Result.Failure<UpdateProgressResponse>(completed.Error);
        }

        return new UpdateProgressResponse(enrolment.Id, applied, ignored, enrolment.ProgressPct, enrolment.CompletedLessons, enrolment.RequiredLessons, completed.Value);
    }
}

internal static class UpdateProgressEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{enrolmentId:guid}/progress", async (Guid enrolmentId, UpdateProgressRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateProgressCommand(enrolmentId, body.Items), ct)).ToHttpResult())
            .WithName("UpdateProgress")
            .WithSummary("Sync per-lesson progress in a batch (offline-safe merge); recomputes progress and completion.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
