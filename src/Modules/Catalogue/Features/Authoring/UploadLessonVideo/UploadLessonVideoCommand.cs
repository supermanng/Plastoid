using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Authoring.UploadLessonVideo;

/// <summary>Creates a media asset and returns the provider's direct-upload URL (resumable on the provider side).</summary>
public sealed record UploadLessonVideoCommand(Guid LessonId) : ICommand<UploadLessonVideoResponse>;

public sealed record UploadLessonVideoResponse(Guid MediaAssetId, string Provider, string UploadId, Uri UploadUrl);

/// <summary>Provider webhook: transcoding finished (or failed). Verified by HMAC signature; idempotent on uploadId.</summary>
public sealed record VideoWebhookCommand(string Provider, string UploadId, string Status, string? PlaybackId, int? DurationSeconds, string? Renditions, string? Error) : ICommand;

public sealed record VideoWebhookPayload(string UploadId, string Status, string? PlaybackId, int? DurationSeconds, string? Renditions, string? Error);
