using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Lms.Domain;

public enum LiveSessionStatus
{
    Scheduled = 0,
    Live = 1,
    Ended = 2,
    Cancelled = 3,
}

public static class LiveSessionSources
{
    public const string Booking = "booking";
    public const string Cohort = "cohort";
}

/// <summary>Blueprint §9.10: Plastoids owns the room record and attendance; the video SDK hosts the call.</summary>
[Audited]
public sealed class LiveSession : Entity
{
    public static readonly TimeSpan JoinWindowBefore = TimeSpan.FromMinutes(15);

    private LiveSession()
    {
    }

    private LiveSession(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string Source { get; private set; } = string.Empty;
    public Guid? BookingId { get; private set; }
    public Guid? CohortId { get; private set; }
    public Guid HostUserId { get; private set; }
    public List<Guid> ParticipantUserIds { get; private set; } = [];
    public string Title { get; private set; } = string.Empty;
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string RoomId { get; private set; } = string.Empty;
    public string RoomUrl { get; private set; } = string.Empty;
    public LiveSessionStatus Status { get; private set; }
    public string? RecordingUrl { get; private set; }

    public static LiveSession Schedule(Guid tenantId, string source, Guid? bookingId, Guid? cohortId, Guid hostUserId, IEnumerable<Guid> participants, string title, DateTime startsAt, DateTime endsAt, string provider, string roomId, string roomUrl)
        => new(NewId(), tenantId)
        {
            Source = source,
            BookingId = bookingId,
            CohortId = cohortId,
            HostUserId = hostUserId,
            ParticipantUserIds = participants.Distinct().ToList(),
            Title = title.Trim(),
            StartsAt = startsAt,
            EndsAt = endsAt,
            Provider = provider,
            RoomId = roomId,
            RoomUrl = roomUrl,
            Status = LiveSessionStatus.Scheduled,
        };

    public bool CanJoin(Guid userId) => userId == HostUserId || ParticipantUserIds.Contains(userId);

    public Result CheckJoinWindow(DateTime utcNow)
    {
        if (Status == LiveSessionStatus.Cancelled)
        {
            return Error.Conflict("session_cancelled", "This session was cancelled.");
        }

        if (utcNow < StartsAt - JoinWindowBefore)
        {
            return Error.Validation("join_too_early", $"The room opens {JoinWindowBefore.TotalMinutes:0} minutes before the start.");
        }

        if (utcNow > EndsAt.AddMinutes(30))
        {
            return Error.Validation("session_over", "This session has ended.");
        }

        return Result.Success();
    }

    public void Move(DateTime startsAt, DateTime endsAt)
    {
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public void Cancel() => Status = LiveSessionStatus.Cancelled;

    public void MarkLive()
    {
        if (Status == LiveSessionStatus.Scheduled)
        {
            Status = LiveSessionStatus.Live;
        }
    }

    public void End(DateTime utcNow)
    {
        if (Status is LiveSessionStatus.Scheduled or LiveSessionStatus.Live && utcNow >= EndsAt)
        {
            Status = LiveSessionStatus.Ended;
        }
    }
}

/// <summary>One join→leave span of one attendee, from provider webhooks. Open spans have no LeftAt yet.</summary>
public sealed class AttendanceEvent : Entity
{
    private AttendanceEvent()
    {
    }

    private AttendanceEvent(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid LiveSessionId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }
    public int DurationSeconds { get; private set; }
    public string ProviderEventId { get; private set; } = string.Empty;

    public static AttendanceEvent Join(Guid tenantId, Guid liveSessionId, Guid userId, DateTime joinedAt, string providerEventId)
        => new(NewId(), tenantId) { LiveSessionId = liveSessionId, UserId = userId, JoinedAt = joinedAt, ProviderEventId = providerEventId };

    public bool IsOpen => LeftAt is null;

    public void Leave(DateTime leftAt)
    {
        if (LeftAt is not null)
        {
            return;
        }

        LeftAt = leftAt < JoinedAt ? JoinedAt : leftAt;
        DurationSeconds = (int)(LeftAt.Value - JoinedAt).TotalSeconds;
    }
}
