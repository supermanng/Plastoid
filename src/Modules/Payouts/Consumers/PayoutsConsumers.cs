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

/// <summary>
/// payments.payment-succeeded.v1 with a payee → an earning: course sales hold 7 days; sessions wait for Booking's 72-hour release.
/// A provider sale (ADR 0019) records the organisation's net and, when a tutor is engaged with a share, a second earning for the tutor.
/// </summary>
internal sealed class PaymentSucceededConsumer(PayoutsDbContext db, IOptions<PayoutOptions> options, IClock clock) : IIntegrationEventHandler<PaymentSucceeded>
{
    public async Task HandleAsync(PaymentSucceeded @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (@event.Purpose is not (PaymentPurposes.Course or PaymentPurposes.Booking))
        {
            return;
        }

        var key = "payment:" + @event.PaymentIntentId.ToString("N");
        var now = clock.UtcNow;
        var source = @event.Purpose == PaymentPurposes.Course ? EarningSources.Course : EarningSources.Booking;
        var releasesAt = @event.Purpose == PaymentPurposes.Course ? now.AddDays(options.Value.CourseSaleHoldDays) : (DateTime?)null;
        if (@event.PayeeType == PayeeTypes.Organisation && @event.PayeeOrganisationId is { } organisation)
        {
            await RecordOnceAsync(envelope.TenantId, PayeeTypes.Organisation, organisation, source, @event, key, @event.PayeeNet ?? @event.Amount, now, releasesAt, cancellationToken);
            if (@event.PayeeUserId is { } engagedTutor && @event.TutorShare is { } share && !share.IsZero)
            {
                await RecordOnceAsync(envelope.TenantId, PayeeTypes.Tutor, engagedTutor, source, @event, key + ":tutor", share, now, releasesAt, cancellationToken);
            }

            return;
        }

        if (@event.PayeeUserId is { } tutor)
        {
            await RecordOnceAsync(envelope.TenantId, PayeeTypes.Tutor, tutor, source, @event, key, @event.PayeeNet ?? @event.Amount, now, releasesAt, cancellationToken);
        }
    }

    private async Task RecordOnceAsync(Guid tenantId, string payeeType, Guid payeeId, string source, PaymentSucceeded @event, string key, Plastoids.SharedKernel.Domain.Money net, DateTime now, DateTime? releasesAt, CancellationToken ct)
    {
        if (await db.Earnings.AnyAsync(e => e.SourceKey == key, ct))
        {
            return;
        }

        db.Earnings.Add(Earning.Record(tenantId, payeeType, payeeId, source, @event.PaymentIntentId, @event.ReferenceId, key, @event.Amount, net, now, releasesAt));
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

/// <summary>payments.refund-issued.v1: the payees' shares of a refunded sale are no longer payable (both rows of a provider sale).</summary>
internal sealed class RefundIssuedConsumer(PayoutsDbContext db) : IIntegrationEventHandler<RefundIssued>
{
    public async Task HandleAsync(RefundIssued @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var earnings = await db.Earnings.Where(e => e.SourceId == @event.OriginalPaymentIntentId).ToListAsync(cancellationToken);
        foreach (var earning in earnings)
        {
            earning.ApplyRefund(@event.Amount);
        }
    }
}

/// <summary>sponsorship.seat-allocated.v1 (ADR 0021): a sponsored seat earns like a course sale (7-day hold); idempotent on the allocation's source keys.</summary>
internal sealed class SeatAllocatedConsumer(PayoutsDbContext db, IOptions<PayoutOptions> options, IClock clock) : IIntegrationEventHandler<SeatAllocated>
{
    public async Task HandleAsync(SeatAllocated @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var earnings = SeatEarnings.Build(envelope.TenantId, @event, clock.UtcNow, options.Value.CourseSaleHoldDays);
        var keys = earnings.Select(e => e.SourceKey).ToList();
        var existing = await db.Earnings.Where(e => keys.Contains(e.SourceKey)).Select(e => e.SourceKey).ToListAsync(cancellationToken);
        foreach (var earning in earnings.Where(e => !existing.Contains(e.SourceKey, StringComparer.Ordinal)))
        {
            db.Earnings.Add(earning);
        }
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
        var earning = Earning.Record(envelope.TenantId, PayeeTypes.Tutor, @event.TutorUserId, EarningSources.Programme, @event.ProgrammeId, @event.ProgrammeId, key, @event.Amount, @event.Amount, now, now);
        earning.Release(now);
        db.Earnings.Add(earning);
    }
}
