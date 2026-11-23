using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Infrastructure.Integrations;

public sealed record LiveRoom(string RoomId, Uri RoomUrl);

public sealed record JoinToken(string Token, Uri JoinUrl, DateTime ExpiresAt);

/// <summary>One join/leave fact from the provider. ParticipantUserId is the Plastoids user the token was issued to.</summary>
public sealed record AttendanceSignal(string EventId, string RoomId, Guid ParticipantUserId, string Kind, DateTime At);

/// <summary>Video SDK adapter (blueprint §9.10: Daily, 100ms or Zoom Video SDK). Plastoids owns the room UI and attendance logic.</summary>
public interface ILiveVideoProvider
{
    string Name { get; }
    Task<Result<LiveRoom>> CreateRoomAsync(Guid liveSessionId, DateTime startsAt, DateTime endsAt, CancellationToken cancellationToken);
    Task<Result<JoinToken>> IssueJoinTokenAsync(string roomId, Guid userId, bool isHost, TimeSpan validFor, DateTime utcNow, CancellationToken cancellationToken);
    bool VerifyWebhookSignature(string payload, string signature);
    Result<IReadOnlyList<AttendanceSignal>> ParseAttendanceWebhook(string payload);
}

/// <summary>Fake rooms under a local domain; HMAC-SHA256 webhooks signed with Secrets:LiveVideoWebhookSecret (default "fake-live-webhook-secret").</summary>
public sealed class FakeLiveVideoProvider(Secrets.ISecretProvider secrets) : ILiveVideoProvider
{
    public const string WebhookSecretName = "LiveVideoWebhookSecret";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public string Name => "fake";

    public Task<Result<LiveRoom>> CreateRoomAsync(Guid liveSessionId, DateTime startsAt, DateTime endsAt, CancellationToken cancellationToken)
    {
        // The last 16 hex chars are the random half of a UUIDv7; the first half is the timestamp and collided when a cohort schedule created several sessions in one millisecond (unique room_id index).
        var roomId = "room_" + liveSessionId.ToString("N")[^16..];
        return Task.FromResult(Result.Success(new LiveRoom(roomId, new Uri($"https://live.plastoids.local/{roomId}"))));
    }

    public Task<Result<JoinToken>> IssueJoinTokenAsync(string roomId, Guid userId, bool isHost, TimeSpan validFor, DateTime utcNow, CancellationToken cancellationToken)
    {
        var expires = utcNow.Add(validFor);
        var payload = $"{roomId}:{userId:N}:{(isHost ? "host" : "participant")}:{new DateTimeOffset(expires).ToUnixTimeSeconds()}";
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=') + "." + Sign(payload)[..24];
        return Task.FromResult(Result.Success(new JoinToken(token, new Uri($"https://live.plastoids.local/{roomId}?t={Uri.EscapeDataString(token)}"), expires)));
    }

    public bool VerifyWebhookSignature(string payload, string signature)
        => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(payload)), Encoding.ASCII.GetBytes(signature ?? string.Empty));

    public string Sign(string payload)
        => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secrets.Get(WebhookSecretName) ?? "fake-live-webhook-secret"), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    public Result<IReadOnlyList<AttendanceSignal>> ParseAttendanceWebhook(string payload)
    {
        try
        {
            var signals = JsonSerializer.Deserialize<List<AttendanceSignal>>(payload, Json);
            return signals is null ? Error.Validation("webhook_payload_invalid", "Body must be an array of attendance signals.") : signals;
        }
        catch (JsonException)
        {
            return Error.Validation("webhook_payload_invalid", "Body must be an array of attendance signals.");
        }
    }

    /// <summary>Builds a signed attendance webhook for tests and local tooling.</summary>
    public (string Payload, string Signature) BuildWebhook(IEnumerable<AttendanceSignal> signals)
    {
        var payload = JsonSerializer.Serialize(signals, Json);
        return (payload, Sign(payload));
    }
}

internal sealed class DailyLiveVideoProvider(Secrets.ISecretProvider secrets) : ILiveVideoProvider
{
    public string Name => "daily";
    private Result<T> NotImplemented<T>() { _ = secrets.GetRequired("DailyApiKey"); return Error.Failure("live_provider_not_implemented", "Daily.co adapter is a stub."); }
    public Task<Result<LiveRoom>> CreateRoomAsync(Guid liveSessionId, DateTime startsAt, DateTime endsAt, CancellationToken cancellationToken) => Task.FromResult(NotImplemented<LiveRoom>());
    public Task<Result<JoinToken>> IssueJoinTokenAsync(string roomId, Guid userId, bool isHost, TimeSpan validFor, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(NotImplemented<JoinToken>());
    public bool VerifyWebhookSignature(string payload, string signature) => false;
    public Result<IReadOnlyList<AttendanceSignal>> ParseAttendanceWebhook(string payload) => NotImplemented<IReadOnlyList<AttendanceSignal>>();
}
