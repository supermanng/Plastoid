using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Modules.Booking.Contracts;
using Plastoids.Modules.Booking.Domain;
using Plastoids.Modules.Booking.Persistence;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Booking.Services;

/// <summary>Confirmation, cancellation side effects (refund, strike, package return) and notifications, shared by slices, the consumer and jobs.</summary>
internal sealed class BookingLifecycle(BookingDbContext db, IPaymentOperations payments, IOutbox outbox, IClock clock)
{
    public void Confirm(Domain.Booking booking)
    {
        if (!booking.Confirm(clock.UtcNow))
        {
            return;
        }

        outbox.Enqueue(new BookingConfirmed(booking.Id, booking.Id, booking.TutorId, booking.TutorUserId, booking.LearnerId, booking.SlotStart, booking.SlotEnd, booking.Format));
        var data = Data(booking);
        outbox.Enqueue(NotificationRequested.To(booking.LearnerId, "booking.confirmed.learner", data));
        outbox.Enqueue(NotificationRequested.To(booking.TutorUserId, "booking.confirmed.tutor", data));
    }

    public async Task<Result<CancellationOutcome>> CancelAsync(Domain.Booking booking, string actor, string reason, bool tutorNoShow, string refundDestination, CancellationToken ct)
    {
        var wasPaid = booking.Status == BookingStatus.Confirmed;
        var outcome = booking.Cancel(actor, reason, tutorNoShow, clock.UtcNow);
        if (outcome.IsFailure)
        {
            return outcome;
        }

        if (wasPaid)
        {
            if (booking.PackageId is { } packageId)
            {
                var package = await db.Packages.SingleAsync(p => p.Id == packageId, ct);
                if (outcome.Value.RefundPercent == 100)
                {
                    package.ReturnSession();
                }
                else if (outcome.Value.RefundPercent > 0)
                {
                    // Half a package session cannot be returned; credit the learner for the partial value instead.
                    var credit = await payments.RefundByPolicyAsync(new PolicyRefundRequest(package.PaymentIntentId!.Value, package.EffectivePricePerSession.Percent(outcome.Value.RefundPercent, MidpointRounding.ToEven), $"Booking {booking.Id:N}: {reason}", RefundDestinations.Credit, booking.LearnerId), ct);
                    if (credit.IsFailure)
                    {
                        return Result.Failure<CancellationOutcome>(credit.Error);
                    }
                }
            }
            else if (booking.PaymentIntentId is { } intentId && outcome.Value.RefundPercent > 0)
            {
                var refund = await payments.RefundByPolicyAsync(new PolicyRefundRequest(intentId, booking.RefundAmount, $"Booking {booking.Id:N} cancelled by {actor}: {reason}", refundDestination, booking.LearnerId), ct);
                if (refund.IsFailure)
                {
                    return Result.Failure<CancellationOutcome>(refund.Error);
                }
            }

            var hold = await db.Holds.SingleOrDefaultAsync(h => h.BookingId == booking.Id, ct);
            hold?.Void();
        }

        if (outcome.Value.TutorStrike)
        {
            var now = clock.UtcNow;
            db.Strikes.Add(TutorStrike.Record(booking.TenantId, booking.TutorId, booking.Id, reason, now));
            var since = now - TutorStrike.Window;
            var strikes = await db.Strikes.CountAsync(s => s.TutorId == booking.TutorId && s.OccurredAt >= since, ct) + 1;
            outbox.Enqueue(new TutorStrikeRecorded(booking.TutorId, booking.TutorId, booking.Id, strikes, strikes >= TutorStrike.ReviewThreshold));
        }

        outbox.Enqueue(new BookingCancelled(booking.Id, booking.Id, actor, outcome.Value.RefundPercent, tutorNoShow));
        var data = Data(booking);
        data["refundPercent"] = outcome.Value.RefundPercent.ToString(System.Globalization.CultureInfo.InvariantCulture);
        outbox.Enqueue(NotificationRequested.To(booking.LearnerId, "booking.cancelled.learner", data));
        outbox.Enqueue(NotificationRequested.To(booking.TutorUserId, "booking.cancelled.tutor", data));
        return outcome;
    }

    public static Dictionary<string, string> Data(Domain.Booking booking) => new()
    {
        ["bookingId"] = booking.Id.ToString("D"),
        ["slotStart"] = booking.SlotStart.ToString("O"),
        ["slotEnd"] = booking.SlotEnd.ToString("O"),
        ["format"] = booking.Format,
        ["amount"] = booking.Price.AmountKobo.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
