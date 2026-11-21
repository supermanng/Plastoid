using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Booking.Persistence;
using Plastoids.Modules.Booking.Services;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Booking.Consumers;

/// <summary>payments.payment-succeeded.v1 with purpose=booking confirms the booking or activates the package the intent paid for.</summary>
internal sealed class PaymentSucceededConsumer(BookingDbContext db, BookingLifecycle lifecycle, IClock clock) : IIntegrationEventHandler<PaymentSucceeded>
{
    public async Task HandleAsync(PaymentSucceeded @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (@event.Purpose != PaymentPurposes.Booking)
        {
            return;
        }

        var booking = await db.Bookings.SingleOrDefaultAsync(b => b.Id == @event.ReferenceId, cancellationToken);
        if (booking is not null)
        {
            booking.AttachPayment(@event.PaymentIntentId);
            lifecycle.Confirm(booking);
            return;
        }

        var package = await db.Packages.SingleOrDefaultAsync(p => p.Id == @event.ReferenceId, cancellationToken);
        package?.AttachPayment(@event.PaymentIntentId);
        package?.Activate(clock.UtcNow);
    }
}
