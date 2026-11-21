using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.BackOffice.Domain;
using Plastoids.Modules.BackOffice.Persistence;
using Plastoids.Modules.BackOffice.Services;
using Plastoids.Modules.Booking.Contracts;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payouts.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.Modules.Tutors.Contracts;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.BackOffice.Consumers;

internal sealed class PaymentSucceededProjector(BackOfficeDbContext db, IClock clock) : IIntegrationEventHandler<PaymentSucceeded>
{
    public Task HandleAsync(PaymentSucceeded @event, IntegrationEventEnvelope envelope, CancellationToken ct)
        => MetricProjection.BumpAsync(db, envelope.TenantId, Metrics.Gmv, @event.Amount, clock.UtcNow, ct);
}

internal sealed class RefundIssuedProjector(BackOfficeDbContext db, IClock clock) : IIntegrationEventHandler<RefundIssued>
{
    public Task HandleAsync(RefundIssued @event, IntegrationEventEnvelope envelope, CancellationToken ct)
        => MetricProjection.BumpAsync(db, envelope.TenantId, Metrics.Refunds, @event.Amount, clock.UtcNow, ct);
}

internal sealed class UserRegisteredProjector(BackOfficeDbContext db, IClock clock) : IIntegrationEventHandler<UserRegistered>
{
    public Task HandleAsync(UserRegistered @event, IntegrationEventEnvelope envelope, CancellationToken ct)
        => @event.Role == "learner" ? MetricProjection.BumpAsync(db, envelope.TenantId, Metrics.LearnersRegistered, null, clock.UtcNow, ct) : Task.CompletedTask;
}

internal sealed class OrganisationVerifiedProjector(BackOfficeDbContext db, IClock clock) : IIntegrationEventHandler<OrganisationVerified>
{
    public Task HandleAsync(OrganisationVerified @event, IntegrationEventEnvelope envelope, CancellationToken ct)
        => @event.Type == "sponsor" ? MetricProjection.BumpAsync(db, envelope.TenantId, Metrics.SponsorsVerified, null, clock.UtcNow, ct) : Task.CompletedTask;
}

internal sealed class TutorVerifiedProjector(BackOfficeDbContext db, IClock clock) : IIntegrationEventHandler<TutorVerified>
{
    public Task HandleAsync(TutorVerified @event, IntegrationEventEnvelope envelope, CancellationToken ct)
        => MetricProjection.BumpAsync(db, envelope.TenantId, Metrics.TutorsVerified, null, clock.UtcNow, ct);
}

internal sealed class EnrolmentCreatedProjector(BackOfficeDbContext db, IClock clock) : IIntegrationEventHandler<EnrolmentCreated>
{
    public Task HandleAsync(EnrolmentCreated @event, IntegrationEventEnvelope envelope, CancellationToken ct)
        => MetricProjection.BumpAsync(db, envelope.TenantId, Metrics.EnrolmentsCreated, null, clock.UtcNow, ct);
}

internal sealed class EnrolmentCompletedProjector(BackOfficeDbContext db, IClock clock) : IIntegrationEventHandler<EnrolmentCompleted>
{
    public Task HandleAsync(EnrolmentCompleted @event, IntegrationEventEnvelope envelope, CancellationToken ct)
        => MetricProjection.BumpAsync(db, envelope.TenantId, Metrics.EnrolmentsCompleted, null, clock.UtcNow, ct);
}

internal sealed class ProgrammeFundedProjector(BackOfficeDbContext db, IClock clock) : IIntegrationEventHandler<ProgrammeFunded>
{
    public Task HandleAsync(ProgrammeFunded @event, IntegrationEventEnvelope envelope, CancellationToken ct)
        => MetricProjection.BumpAsync(db, envelope.TenantId, Metrics.ProgrammesFunded, null, clock.UtcNow, ct);
}

internal sealed class ProgrammePublishedProjector(BackOfficeDbContext db, IClock clock) : IIntegrationEventHandler<ProgrammePublished>
{
    public Task HandleAsync(ProgrammePublished @event, IntegrationEventEnvelope envelope, CancellationToken ct)
        => MetricProjection.BumpAsync(db, envelope.TenantId, Metrics.ProgrammesPublished, null, clock.UtcNow, ct);
}

internal sealed class BatchApprovedProjector(BackOfficeDbContext db, IClock clock) : IIntegrationEventHandler<BatchApproved>
{
    public Task HandleAsync(BatchApproved @event, IntegrationEventEnvelope envelope, CancellationToken ct)
        => MetricProjection.BumpAsync(db, envelope.TenantId, Metrics.PayoutBatchesApproved, @event.TotalAmount, clock.UtcNow, ct);
}

internal sealed class PayoutSettledProjector(BackOfficeDbContext db, IClock clock) : IIntegrationEventHandler<PayoutSettled>
{
    public Task HandleAsync(PayoutSettled @event, IntegrationEventEnvelope envelope, CancellationToken ct)
        => MetricProjection.BumpAsync(db, envelope.TenantId, Metrics.PayoutsSettled, @event.Amount, clock.UtcNow, ct);
}

internal sealed class PayoutFailedProjector(BackOfficeDbContext db, IClock clock) : IIntegrationEventHandler<PayoutFailed>
{
    public async Task HandleAsync(PayoutFailed @event, IntegrationEventEnvelope envelope, CancellationToken ct)
    {
        var now = clock.UtcNow;
        await MetricProjection.BumpAsync(db, envelope.TenantId, Metrics.PayoutsFailed, @event.Amount, now, ct);
        if (!await db.Alerts.AnyAsync(a => a.Kind == AlertKinds.PayoutFailed && a.ReferenceId == @event.PayoutId, ct))
        {
            db.Alerts.Add(RiskAlert.Raise(envelope.TenantId, AlertKinds.PayoutFailed, "medium", $"Payout {@event.PayoutId:N} {(@event.Reversed ? "reversed" : "failed")}: {@event.Reason}", @event.PayoutId, now));
        }
    }
}

/// <summary>ledger.shortfall-detected.v1 → high-severity alert (payouts are frozen by the ledger control).</summary>
internal sealed class ShortfallProjector(BackOfficeDbContext db, IClock clock) : IIntegrationEventHandler<ShortfallDetected>
{
    public async Task HandleAsync(ShortfallDetected @event, IntegrationEventEnvelope envelope, CancellationToken ct)
    {
        if (await db.Alerts.AnyAsync(a => a.Kind == AlertKinds.Shortfall && a.ResolvedAt == null, ct))
        {
            return;
        }

        db.Alerts.Add(RiskAlert.Raise(envelope.TenantId, AlertKinds.Shortfall, "critical", $"Client-funds shortfall of {Money.Kobo(@event.ShortfallKobo)} on {@event.AsOf:yyyy-MM-dd}; payouts frozen", null, clock.UtcNow));
    }
}

internal sealed class TutorStrikeProjector(BackOfficeDbContext db, IClock clock) : IIntegrationEventHandler<TutorStrikeRecorded>
{
    public async Task HandleAsync(TutorStrikeRecorded @event, IntegrationEventEnvelope envelope, CancellationToken ct)
    {
        var now = clock.UtcNow;
        await MetricProjection.BumpAsync(db, envelope.TenantId, Metrics.TutorStrikes, null, now, ct);
        if (@event.ReviewFlagged && !await db.Alerts.AnyAsync(a => a.Kind == AlertKinds.TutorStrikes && a.ReferenceId == @event.TutorId && a.ResolvedAt == null, ct))
        {
            db.Alerts.Add(RiskAlert.Raise(envelope.TenantId, AlertKinds.TutorStrikes, "high", $"Tutor {@event.TutorId:N} reached {@event.StrikesInLast90Days} strikes in 90 days; review required", @event.TutorId, now));
        }
    }
}
