using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Domain;

public enum MediaStatus
{
    PendingUpload = 0,
    Processing = 1,
    Ready = 2,
    Failed = 3,
}

/// <summary>Video asset lifecycle: direct upload → provider transcode webhook → ready with a playback id (story C2).</summary>
public sealed class MediaAsset : Entity
{
    private MediaAsset()
    {
    }

    private MediaAsset(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid LessonId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string UploadId { get; private set; } = string.Empty;
    public MediaStatus Status { get; private set; }
    public string? PlaybackId { get; private set; }
    public int? DurationSeconds { get; private set; }
    public string? Renditions { get; private set; }
    public string? FailureReason { get; private set; }

    public static MediaAsset Create(Guid tenantId, Guid lessonId, string provider, string uploadId)
        => new(NewId(), tenantId) { LessonId = lessonId, Provider = provider, UploadId = uploadId, Status = MediaStatus.PendingUpload };

    public void MarkProcessing() => Status = MediaStatus.Processing;

    public void MarkReady(string playbackId, int? durationSeconds, string? renditions)
    {
        Status = MediaStatus.Ready;
        PlaybackId = playbackId;
        DurationSeconds = durationSeconds;
        Renditions = renditions;
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        Status = MediaStatus.Failed;
        FailureReason = reason.Length > 500 ? reason[..500] : reason;
    }
}
