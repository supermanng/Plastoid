using Plastoids.Infrastructure.Storage;
using Plastoids.Infrastructure.Time;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.Modules.Tutors.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Tutors.Features.Onboarding.SubmitDemoLesson;

internal sealed class SubmitDemoLessonHandler(TutorsDbContext db, TutorAccess access, IBlobStorage storage, IClock clock)
    : ICommandHandler<SubmitDemoLessonCommand, SubmitDemoLessonResponse>
{
    public async Task<Result<SubmitDemoLessonResponse>> Handle(SubmitDemoLessonCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MineAsync(cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<SubmitDemoLessonResponse>(mine.Error);
        }

        var profile = mine.Value;
        var now = clock.UtcNow;
        var key = $"demo-lessons/{profile.Id:N}/{now:yyyyMMddHHmmss}.{(request.ContentType == "video/webm" ? "webm" : "mp4")}";
        var submitted = profile.SubmitDemoLesson(key, now);
        if (submitted.IsFailure)
        {
            return Result.Failure<SubmitDemoLessonResponse>(submitted.Error);
        }

        var due = WorkingDays.Add(now, ReviewQueueItem.SubjectSlaWorkingDays);
        db.ReviewQueue.Add(ReviewQueueItem.Enqueue(profile.TenantId, profile.Id, ReviewKinds.Subject, profile.Id, now, due));
        var upload = await storage.GetPresignedUploadUrlAsync(key, request.ContentType, TimeSpan.FromHours(2), cancellationToken);
        return new SubmitDemoLessonResponse(upload, due);
    }
}
