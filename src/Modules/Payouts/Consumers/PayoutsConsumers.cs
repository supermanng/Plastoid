using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Plastoids.Modules.Booking.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payouts.Domain;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.Modules.Payouts.Services;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payouts.Consumers;

/// <summary>payments.payment-succeeded.v1 with a payee → an earning: course sales hold 7 days; sessions wait for Booking's 72-hour release.</summary>
internal sealed class PaymentSucceededConsumer(PayoutsDbContext db, IOptions<PayoutOptions> options, IClock clock) : IIntegrationEventHandler<PaymentSucceeded>
{
    public async Task HandleAsync(PaymentSucceeded @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (@event.PayeeUserId is not { } tutor || @event.Purpose is not (PaymentPurposes.Course or PaymentPurposes.Booking))
        {
            return;
        }

        var key = "payment:" + @event.PaymentIntentId.ToString("N");
        if (await db.Earnings.AnyAsync(e => e.SourceKey == key, cancellationToken))
        {
            return;
        }

        var now = clock.UtcNow;
        var net = @event.PayeeNet ?? @event.Amount;
        var releasesAt = @event.Purpose == PaymentPurposes.Course ? now.AddDays(options.Value.CourseSaleHoldDays) : (DateTime?)null;
        db.Earnings.Add(Earning.Record(envelope.TenantId, tutor, @event.Purpose == PaymentPurposes.Course ? EarningSources.Course : EarningSources.Booking, @event.PaymentIntentId, @event.ReferenceId, key, @event.Amount, net, now, releasesAt));
    }
}

/// <summary>booking.earning-released.v1: the 72-hour dispute window passed; the session earning becomes payable.</summary>
internal sealed class EarningReleasedConsumer(PayoutsDbContext db, IClock clock) : IIntegrationEventHandler<EarningReleased>
{
    public async Task HandleAsync(EarningReleased @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var earnings = await db.Earnings.Where(e => e.ReferenceId == @event.BookingId && e.SourceType == EarningSources.Booking && e.Status == EarningStatus.Held).ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        foreach (var earning in earnings)
        {
            earning.Release(now);
        }
    }
}

/// <summary>payments.refund-issued.v1: the tutor's share of a refunded sale is no longer payable.</summary>
internal sealed class RefundIssuedConsumer(PayoutsDbContext db) : IIntegrationEventHandler<RefundIssued>
{
    public async Task HandleAsync(RefundIssued @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var earning = await db.Earnings.SingleOrDefaultAsync(e => e.SourceId == @event.OriginalPaymentIntentId, cancellationToken);
        earning?.ApplyRefund(@event.Amount);
    }
}

/// <summary>sponsorship.tutor-earnings-accrued.v1: a delivered programme month; payable at once (bi-weekly run in arrears).</summary>
internal sealed class TutorEarningsAccruedConsumer(PayoutsDbContext db, IClock clock) : IIntegrationEventHandler<TutorEarningsAccrued>
{
    public async Task HandleAsync(TutorEarningsAccrued @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var key = $"programme:{@event.ProgrammeId:N}:month:{@event.MonthIndex}";
        if (await db.Earnings.AnyAsync(e => e.SourceKey == key, cancellationToken))
        {
            return;
        }

        var now = clock.UtcNow;
        var earning = Earning.Record(envelope.TenantId, @event.TutorUserId, EarningSources.Programme, @event.ProgrammeId, @event.ProgrammeId, key, @event.Amount, @event.Amount, now, now);
        earning.Release(now);
        db.Earnings.Add(earning);
    }
}
