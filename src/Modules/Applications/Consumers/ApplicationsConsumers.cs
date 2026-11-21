using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Applications.Domain;
using Plastoids.Modules.Applications.Persistence;
using Plastoids.Modules.Applications.Services;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Applications.Consumers;

/// <summary>sponsorship.seat-released.v1 → backfill from the waitlist.</summary>
internal sealed class SeatReleasedConsumer(OfferService offers, IClock clock) : IIntegrationEventHandler<SeatReleased>
{
    public Task HandleAsync(SeatReleased @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
        => offers.BackfillAsync(@event.ProgrammeId, clock.UtcNow, cancellationToken);
}

/// <summary>payments.payment-succeeded.v1 (co_pay) → the learner's share is paid; the seat is confirmed and the offer accepted.</summary>
internal sealed class CoPayPaymentConsumer(ApplicationsDbContext db, IProgrammeReader programmes, OfferService offers) : IIntegrationEventHandler<PaymentSucceeded>
{
    public async Task HandleAsync(PaymentSucceeded @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (@event.Purpose != PaymentPurposes.CoPay)
        {
            return;
        }

        var application = await db.Applications.SingleOrDefaultAsync(a => a.CoPayIntentId == @event.PaymentIntentId, cancellationToken);
        if (application is null || !application.ConfirmCoPay())
        {
            return;
        }

        var programme = await programmes.GetAsync(application.ProgrammeId, cancellationToken)
            ?? throw new InvalidOperationException($"Programme {application.ProgrammeId} missing for co-pay {@event.PaymentIntentId}.");
        offers.EmitAccepted(application, programme);
    }
}

/// <summary>sponsorship.programme-funded.v1 → local programme reference for the review queue summary.</summary>
internal sealed class ProgrammeFundedConsumer(ApplicationsDbContext db) : IIntegrationEventHandler<ProgrammeFunded>
{
    public async Task HandleAsync(ProgrammeFunded @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var existing = await db.Programmes.SingleOrDefaultAsync(p => p.ProgrammeId == @event.ProgrammeId, cancellationToken);
        if (existing is null)
        {
            db.Programmes.Add(ProgrammeRef.Record(envelope.TenantId, @event.ProgrammeId, @event.SeatsFunded));
        }
        else
        {
            existing.Update(@event.SeatsFunded);
        }
    }
}
