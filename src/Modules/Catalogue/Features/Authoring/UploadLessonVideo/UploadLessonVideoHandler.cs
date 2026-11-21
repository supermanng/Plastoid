using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Catalogue.Features.Authoring.UploadLessonVideo;

internal sealed class UploadLessonVideoHandler(CatalogueDbContext db, CourseAccess access, IVideoProvider video) : ICommandHandler<UploadLessonVideoCommand, UploadLessonVideoResponse>
{
    public async Task<Result<UploadLessonVideoResponse>> Handle(UploadLessonVideoCommand request, CancellationToken cancellationToken)
    {
        var lesson = await db.Lessons.SingleOrDefaultAsync(l => l.Id == request.LessonId, cancellationToken);
        if (lesson is null)
        {
            return Error.NotFound("lesson_not_found", "No such lesson.");
        }

        var owned = await access.OwnedAsync(lesson.CourseId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<UploadLessonVideoResponse>(owned.Error);
        }

        if (lesson.Type != LessonTypes.Video)
        {
            return Error.Validation("not_a_video_lesson", "Only video lessons accept uploads.");
        }

        var upload = await video.CreateDirectUploadAsync(lesson.Id.ToString("N"), cancellationToken);
        if (upload.IsFailure)
        {
            return Result.Failure<UploadLessonVideoResponse>(upload.Error);
        }

        var asset = MediaAsset.Create(lesson.TenantId, lesson.Id, video.Name, upload.Value.UploadId);
        db.MediaAssets.Add(asset);
        lesson.AttachMedia(asset.Id);
        return new UploadLessonVideoResponse(asset.Id, video.Name, upload.Value.UploadId, upload.Value.UploadUrl);
    }
}

internal sealed class VideoWebhookHandler(CatalogueDbContext db) : ICommandHandler<VideoWebhookCommand>
{
    public async Task<Result> Handle(VideoWebhookCommand request, CancellationToken cancellationToken)
    {
        var asset = await db.MediaAssets.SingleOrDefaultAsync(a => a.Provider == request.Provider && a.UploadId == request.UploadId, cancellationToken);
        if (asset is null)
        {
            return Error.NotFound("media_asset_not_found", "Unknown upload id.");
        }

        switch (request.Status)
        {
            case "ready":
                asset.MarkReady(request.PlaybackId!, request.DurationSeconds, request.Renditions);
                break;
            case "failed":
                asset.MarkFailed(request.Error ?? "Transcoding failed.");
                break;
            default:
                asset.MarkProcessing();
                break;
        }

        return Result.Success();
    }
}
