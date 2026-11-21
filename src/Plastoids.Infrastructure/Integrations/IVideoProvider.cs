using System.Security.Cryptography;
using System.Text;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Infrastructure.Integrations;

public sealed record DirectUpload(string UploadId, Uri UploadUrl, DateTime ExpiresAt);

/// <summary>Video on demand (Mux-style): direct upload → transcode webhook → signed HLS playback (blueprint §17).</summary>
public interface IVideoProvider
{
    string Name { get; }
    Task<Result<DirectUpload>> CreateDirectUploadAsync(string correlationKey, CancellationToken cancellationToken);
    bool VerifyWebhookSignature(string payload, string signature);
    Uri SignedPlaybackUrl(string playbackId, TimeSpan validFor, DateTime utcNow);
}

/// <summary>Fake provider: uploads go to blob storage through a presigned URL; the webhook is signed with a shared secret so tests can simulate transcoding.</summary>
public sealed class FakeVideoProvider(Storage.IBlobStorage storage, Secrets.ISecretProvider secrets) : IVideoProvider
{
    public const string WebhookSecretName = "VideoWebhookSecret";
    public string Name => "fake";

    public async Task<Result<DirectUpload>> CreateDirectUploadAsync(string correlationKey, CancellationToken cancellationToken)
    {
        var uploadId = "upl_" + Guid.CreateVersion7().ToString("N");
        var url = await storage.GetPresignedUploadUrlAsync($"video-uploads/{correlationKey}/{uploadId}.mp4", "video/mp4", TimeSpan.FromHours(1), cancellationToken);
        return new DirectUpload(uploadId, url, DateTime.SpecifyKind(DateTime.UnixEpoch, DateTimeKind.Utc).AddHours(1) /* informational only */);
    }

    public bool VerifyWebhookSignature(string payload, string signature)
    {
        var expected = Sign(payload);
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(signature ?? string.Empty));
    }

    public string Sign(string payload)
        => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secrets.Get(WebhookSecretName) ?? "fake-video-webhook-secret"), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    public Uri SignedPlaybackUrl(string playbackId, TimeSpan validFor, DateTime utcNow)
    {
        var expires = new DateTimeOffset(utcNow.Add(validFor)).ToUnixTimeSeconds();
        var token = Sign($"{playbackId}:{expires}")[..32];
        return new Uri($"https://stream.plastoids.local/{playbackId}.m3u8?exp={expires}&sig={token}");
    }
}

internal sealed class MuxVideoProvider(Secrets.ISecretProvider secrets) : IVideoProvider
{
    public string Name => "mux";

    public Task<Result<DirectUpload>> CreateDirectUploadAsync(string correlationKey, CancellationToken cancellationToken)
    {
        _ = secrets.GetRequired("MuxTokenSecret");
        return Task.FromResult(Result.Failure<DirectUpload>(Error.Failure("video_provider_not_implemented", "Mux integration is a stub.")));
    }

    public bool VerifyWebhookSignature(string payload, string signature) => false;

    public Uri SignedPlaybackUrl(string playbackId, TimeSpan validFor, DateTime utcNow) => new($"https://stream.mux.com/{playbackId}.m3u8");
}
