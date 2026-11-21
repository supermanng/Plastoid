using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Booking.Domain;

/// <summary>A tutor no-show. Three within 90 days flag the tutor for review (blueprint §9).</summary>
public sealed class TutorStrike : Entity
{
    public const int ReviewThreshold = 3;
    public static readonly TimeSpan Window = TimeSpan.FromDays(90);

    private TutorStrike()
    {
    }

    private TutorStrike(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid TutorId { get; private set; }
    public Guid BookingId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }

    public static TutorStrike Record(Guid tenantId, Guid tutorId, Guid bookingId, string reason, DateTime utcNow)
        => new(NewId(), tenantId) { TutorId = tutorId, BookingId = bookingId, Reason = reason, OccurredAt = utcNow };
}

public enum HoldStatus
{
    Held = 0,
    Released = 1,
    Voided = 2,
}

/// <summary>Blueprint §18 settlement rules: session earnings become payable 72 h after the session (dispute window).</summary>
public sealed class EarningHold : Entity
{
    public static readonly TimeSpan HoldPeriod = TimeSpan.FromHours(72);

    private EarningHold()
    {
    }

    private EarningHold(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid BookingId { get; private set; }
    public Guid TutorUserId { get; private set; }
    public Money Amount { get; private set; }
    public DateTime ReleasesAt { get; private set; }
    public HoldStatus Status { get; private set; }
    public DateTime? ReleasedAt { get; private set; }

    public static EarningHold Create(Guid tenantId, Guid bookingId, Guid tutorUserId, Money amount, DateTime sessionEnd)
        => new(NewId(), tenantId) { BookingId = bookingId, TutorUserId = tutorUserId, Amount = amount, ReleasesAt = sessionEnd.Add(HoldPeriod), Status = HoldStatus.Held };

    public bool Release(DateTime utcNow)
    {
        if (Status != HoldStatus.Held || utcNow < ReleasesAt)
        {
            return false;
        }

        Status = HoldStatus.Released;
        ReleasedAt = utcNow;
        return true;
    }

    public void Void() => Status = HoldStatus.Voided;
}
