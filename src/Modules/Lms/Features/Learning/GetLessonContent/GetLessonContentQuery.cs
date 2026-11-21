using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.Modules.Lms.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Lms.Features.Learning.GetLessonContent;

/// <summary>Story E1: the lesson as the learner consumes it: signed HLS URL for video, quiz skeleton without answers, resume position.</summary>
public sealed record GetLessonContentQuery(Guid EnrolmentId, Guid LessonId) : IQuery<LessonContent>;

public sealed record QuizQuestionView(int Index, string Prompt, IReadOnlyList<string> Options, bool MultiSelect, bool ShortAnswer);

public sealed record LessonContent(Guid LessonId, string Type, string Title, int DurationMinutes, Uri? PlaybackUrl, JsonElement? Document, int? PassMarkPercent, int? TimeLimitMinutes, IReadOnlyList<QuizQuestionView>? Questions, string ProgressStatus, int PositionSeconds, int Page, decimal PercentComplete);

internal sealed class GetLessonContentHandler(LmsDbContext db, LmsAccess access, ProgressService progress, IVideoProvider video, IClock clock) : IQueryHandler<GetLessonContentQuery, LessonContent>
{
    public async Task<Result<LessonContent>> Handle(GetLessonContentQuery request, CancellationToken cancellationToken)
    {
        var mine = await access.MyEnrolmentAsync(request.EnrolmentId, cancellationToken, requireActive: false);
        if (mine.IsFailure)
        {
            return Result.Failure<LessonContent>(mine.Error);
        }

        var enrolment = mine.Value;
        if (enrolment.Status == Domain.EnrolmentStatus.Withdrawn || enrolment.ExpiresAt < clock.UtcNow)
        {
            return Error.Forbidden("enrolment_access_ended", "Access to this course has ended.");
        }

        var course = await progress.CourseForAsync(enrolment, cancellationToken);
        if (course.IsFailure)
        {
            return Result.Failure<LessonContent>(course.Error);
        }

        var lesson = course.Value.Lessons.SingleOrDefault(l => l.LessonId == request.LessonId);
        if (lesson is null)
        {
            return Error.NotFound("lesson_not_found", "No such lesson in this course version.");
        }

        var state = await db.Progress.AsNoTracking().SingleOrDefaultAsync(p => p.EnrolmentId == enrolment.Id && p.LessonId == lesson.LessonId, cancellationToken);
        Uri? playback = lesson.Type == "video" && lesson.PlaybackId is not null ? video.SignedPlaybackUrl(lesson.PlaybackId, TimeSpan.FromHours(2), clock.UtcNow) : null;
        JsonElement? document = lesson.Type == "doc" ? lesson.Content : null;

        IReadOnlyList<QuizQuestionView>? questions = null;
        int? passMark = null;
        int? timeLimit = null;
        if (lesson.Type == "quiz" && lesson.Content is { } content && content.ValueKind == JsonValueKind.Object)
        {
            passMark = ProgressService.PassMarkOf(lesson);
            timeLimit = ProgressService.TimeLimitOf(lesson);
            var list = new List<QuizQuestionView>();
            if (content.TryGetProperty("questions", out var qs) && qs.ValueKind == JsonValueKind.Array)
            {
                var i = 0;
                foreach (var q in qs.EnumerateArray())
                {
                    var options = q.TryGetProperty("options", out var o) && o.ValueKind == JsonValueKind.Array ? o.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList() : new List<string>();
                    list.Add(new QuizQuestionView(i++, q.TryGetProperty("prompt", out var p) ? p.GetString() ?? string.Empty : string.Empty, options, q.TryGetProperty("correctIndexes", out _), q.TryGetProperty("answer", out _)));
                }
            }

            questions = list;
        }

        return new LessonContent(lesson.LessonId, lesson.Type, lesson.Title, lesson.DurationMinutes, playback, document, passMark, timeLimit, questions,
            (state?.Status ?? Domain.LessonStatus.NotStarted).ToString().ToLowerInvariant(), state?.PositionSeconds ?? 0, state?.Page ?? 0, state?.PercentComplete ?? 0);
    }
}

internal static class GetLessonContentEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/{enrolmentId:guid}/lessons/{lessonId:guid}", async (Guid enrolmentId, Guid lessonId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetLessonContentQuery(enrolmentId, lessonId), ct)).ToHttpResult())
            .WithName("GetLessonContent")
            .WithSummary("Lesson content for an enrolment: signed HLS URL, document, or quiz questions (answers withheld), plus resume state.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
