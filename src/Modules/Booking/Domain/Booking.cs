using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Booking.Domain;

public enum BookingStatus
{
    PendingPayment = 0,
    Confirmed = 1,
    Cancelled = 2,
    Completed = 3,
    NoShow = 4,
}

public static class CancellationActors
{
    public const string Learner = "learner";
    public const string Tutor = "tutor";
    public const string System = "system";
}

public sealed record CancellationOutcome(int RefundPercent, bool TutorStrike);

/// <summary>
/// Blueprint §9 rules: payment captured at booking; learner cancels >24 h before → 100 %, &lt;24 h → 50 %; tutor cancels → 100 %;
/// tutor no-show → 100 % + strike. The slot key (tutor + start) is unique while the booking is live, which is the
/// double-booking guard behind the Redis checkout lock.
/// </summary>
[Audited]
public sealed class Booking : Entity
{
    public static readonly TimeSpan FullRefundNotice = TimeSpan.FromHours(24);
    public const int LateCancellationRefundPercent = 50;

    private Booking()
    {
    }

    private Booking(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid TutorId { get; private set; }
    public Guid TutorUserId { get; private set; }
    public Guid LearnerId { get; private set; }
    public string Format { get; private set; } = string.Empty;
    public DateTime SlotStart { get; private set; }
    public DateTime SlotEnd { get; private set; }
    public int DurationMinutes { get; private set; }
    public Money Price { get; private set; }
    public BookingStatus Status { get; private set; }
    public Guid? PaymentIntentId { get; private set; }
    public Guid? PackageId { get; private set; }
    public string? SlotKey { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancelledBy { get; private set; }
    public string? CancellationReason { get; private set; }
    public int RefundPercent { get; private set; }
    public DateTime? RescheduledFrom { get; private set; }
    public DateTime? Reminder24SentAt { get; private set; }
    public DateTime? Reminder1SentAt { get; private set; }
    public string? Note { get; private set; }

    public static string KeyFor(Guid tutorId, DateTime slotStart) => $"{tutorId:N}:{slotStart:yyyyMMddHHmm}";

    public static Result<Booking> Create(Guid tenantId, Guid tutorId, Guid tutorUserId, Guid learnerId, string format, DateTime slotStart, int durationMinutes, Money price, Guid? packageId, string? note, DateTime utcNow)
    {
        if (slotStart <= utcNow.AddMinutes(30))
        {
            return Error.Validation("slot_too_soon", "Sessions must start at least 30 minutes from now.");
        }

        if (tutorUserId == learnerId)
        {
            return Error.Validation("self_booking", "You cannot book yourself.");
        }

        return new Booking(NewId(), tenantId)
        {
            TutorId = tutorId,
            TutorUserId = tutorUserId,
            LearnerId = learnerId,
            Format = format,
            SlotStart = slotStart,
            SlotEnd = slotStart.AddMinutes(durationMinutes),
            DurationMinutes = durationMinutes,
            Price = price,
            Status = BookingStatus.PendingPayment,
            PackageId = packageId,
            SlotKey = KeyFor(tutorId, slotStart),
            Note = note?.Trim(),
        };
    }

    public void AttachPayment(Guid paymentIntentId) => PaymentIntentId = paymentIntentId;

    /// <summary>Returns true the first time (payment-succeeded webhooks may be redelivered).</summary>
    public bool Confirm(DateTime utcNow)
    {
        if (Status != BookingStatus.PendingPayment)
        {
            return false;
        }

        Status = BookingStatus.Confirmed;
        ConfirmedAt = utcNow;
        return true;
    }

    public bool IsLive => Status is BookingStatus.PendingPayment or BookingStatus.Confirmed;

    /// <summary>Applies the cancellation policy. Only the learner may report a tutor no-show, after the slot started.</summary>
    public Result<CancellationOutcome> Cancel(string actor, string reason, bool tutorNoShow, DateTime utcNow)
    {
        if (!IsLive)
        {
            return Error.Conflict("booking_not_cancellable", $"A {Status.ToString().ToLowerInvariant()} booking cannot be cancelled.");
        }

        var wasPaid = Status == BookingStatus.Confirmed;
        int refundPercent;
        var strike = false;
        if (tutorNoShow)
        {
            if (actor != CancellationActors.Learner && actor != CancellationActors.System)
            {
                return Error.Forbidden("no_show_report_by_learner", "Only the learner (or staff) can report a tutor no-show.");
            }

            if (utcNow < SlotStart.AddMinutes(10))
            {
                return Error.Validation("no_show_too_early", "Report a no-show from 10 minutes after the session was due to start.");
            }

            if (utcNow > SlotEnd.AddHours(48))
            {
                return Error.Validation("no_show_window_closed", "No-shows must be reported within 48 hours of the session.");
            }

            refundPercent = 100;
            strike = true;
            Status = BookingStatus.NoShow;
        }
        else
        {
            if (utcNow >= SlotStart)
            {
                return Error.Conflict("booking_already_started", "The session has already started; report a no-show instead.");
            }

            refundPercent = actor switch
            {
                CancellationActors.Learner => SlotStart - utcNow >= FullRefundNotice ? 100 : LateCancellationRefundPercent,
                _ => 100,
            };
            Status = BookingStatus.Cancelled;
        }

        CancelledAt = utcNow;
        CancelledBy = actor;
        CancellationReason = reason.Trim();
        RefundPercent = wasPaid ? refundPercent : 0;
        SlotKey = null;
        return new CancellationOutcome(refundPercent, strike);
    }

    /// <summary>Learner may move a session with ≥24 h notice; the tutor at any time before it starts.</summary>
    public Result Reschedule(string actor, DateTime newStart, DateTime utcNow)
    {
        if (Status != BookingStatus.Confirmed)
        {
            return Error.Conflict("booking_not_confirmed", "Only confirmed bookings can be rescheduled.");
        }

        if (utcNow >= SlotStart)
        {
            return Error.Conflict("booking_already_started", "The session has already started.");
        }

        if (actor == CancellationActors.Learner && SlotStart - utcNow < FullRefundNotice)
        {
            return Error.Validation("reschedule_notice", "Learners can reschedule up to 24 hours before the session.");
        }

        if (newStart <= utcNow.AddMinutes(30))
        {
            return Error.Validation("slot_too_soon", "The new slot must start at least 30 minutes from now.");
        }

        RescheduledFrom = SlotStart;
        SlotStart = newStart;
        SlotEnd = newStart.AddMinutes(DurationMinutes);
        SlotKey = KeyFor(TutorId, newStart);
        Reminder24SentAt = null;
        Reminder1SentAt = null;
        return Result.Success();
    }

    public bool Complete(DateTime utcNow)
    {
        if (Status != BookingStatus.Confirmed || utcNow < SlotEnd)
        {
            return false;
        }

        Status = BookingStatus.Completed;
        CompletedAt = utcNow;
        return true;
    }

    public void MarkReminder24(DateTime utcNow) => Reminder24SentAt = utcNow;

    public void MarkReminder1(DateTime utcNow) => Reminder1SentAt = utcNow;

    public Money RefundAmount => Price.Percent(RefundPercent, MidpointRounding.ToEven);
}
