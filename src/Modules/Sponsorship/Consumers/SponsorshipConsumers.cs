using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Modules.Applications.Contracts;
using Plastoids.Modules.Certificates.Contracts;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Consumers;

internal static class Funnel
{
    public static async Task BumpAsync(SponsorshipDbContext db, Guid tenantId, Guid programmeId, string stage, DateTime now, CancellationToken ct)
    {
        var funnel = db.Funnels.Local.SingleOrDefault(f => f.ProgrammeId == programmeId) ?? await db.Funnels.SingleOrDefaultAsync(f => f.ProgrammeId == programmeId, ct);
        if (funnel is null)
        {
            funnel = ProgrammeFunnel.Start(tenantId, programmeId, now);
            db.Funnels.Add(funnel);
        }

        funnel.Bump(stage, now);
    }
}

/// <summary>payments.invoice-paid.v1 → programme funded (seats unlocked), sponsorship.programme-funded.v1.</summary>
internal sealed class InvoicePaidConsumer(SponsorshipDbContext db, IOutbox outbox, IClock clock) : IIntegrationEventHandler<InvoicePaid>
{
    public async Task HandleAsync(InvoicePaid @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (@event.ProgrammeId is not { } programmeId)
        {
            return;
        }

        var programme = await db.Programmes.SingleOrDefaultAsync(p => p.Id == programmeId, cancellationToken);
        if (programme is null || await db.Receipts.AnyAsync(r => r.InvoiceId == @event.InvoiceId, cancellationToken))
        {
            return;
        }

        var now = clock.UtcNow;
        db.Receipts.Add(FundingReceipt.Record(envelope.TenantId, programmeId, @event.InvoiceId, @event.Amount, now));
        if (programme.RecordFunding(@event.Amount, now))
        {
            outbox.Enqueue(new ProgrammeFunded(programme.Id, programme.Id, programme.SeatsFundable));
            outbox.Enqueue(NotificationRequested.To(programme.CreatedBy, "sponsorship.programme.funded", new Dictionary<string, string> { ["programmeId"] = programme.Id.ToString("D"), ["name"] = programme.Name, ["seats"] = programme.SeatsFundable.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        }
    }
}

/// <summary>applications.offer-accepted.v1 → seat filled, seat fee earned (entry 7), sponsorship.seat-filled.v1. Lms enrols from the same event.</summary>
internal sealed class OfferAcceptedConsumer(SponsorshipDbContext db, ILedgerService ledger, IOutbox outbox, IClock clock) : IIntegrationEventHandler<OfferAccepted>
{
    public async Task HandleAsync(OfferAccepted @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var seat = await db.Seats.SingleOrDefaultAsync(s => s.Id == @event.SeatId, cancellationToken);
        if (seat is null)
        {
            throw new InvalidOperationException($"Seat {@event.SeatId} for application {@event.ApplicationId} does not exist.");
        }

        if (seat.Status == SeatStatus.Filled && seat.ApplicationId == @event.ApplicationId)
        {
            return;
        }

        var now = clock.UtcNow;
        var filled = seat.Fill(@event.ApplicationId, @event.LearnerId, now);
        if (filled.IsFailure)
        {
            throw new InvalidOperationException($"Seat {seat.Id} could not be filled: {filled.Error.Code}");
        }

        var posted = await ledger.PostAsync(SponsorshipPostings.Entry7SeatFee(seat, now), cancellationToken);
        if (posted.IsFailure)
        {
            throw new InvalidOperationException($"Seat fee entry failed: {posted.Error.Code}");
        }

        await Funnel.BumpAsync(db, envelope.TenantId, seat.ProgrammeId, "enrolled", now, cancellationToken);
        outbox.Enqueue(new SeatFilled(seat.ProgrammeId, seat.ProgrammeId, seat.Id, @event.ApplicationId, @event.LearnerId));
    }
}

internal sealed class ApplicationSubmittedConsumer(SponsorshipDbContext db, IClock clock) : IIntegrationEventHandler<ApplicationSubmitted>
{
    public async Task HandleAsync(ApplicationSubmitted @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!@event.Flagged)
        {
            await Funnel.BumpAsync(db, envelope.TenantId, @event.ProgrammeId, "applied", clock.UtcNow, cancellationToken);
        }
    }
}

internal sealed class OfferSentConsumer(SponsorshipDbContext db, IClock clock) : IIntegrationEventHandler<OfferSent>
{
    public Task HandleAsync(OfferSent @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
        => Funnel.BumpAsync(db, envelope.TenantId, @event.ProgrammeId, "offered", clock.UtcNow, cancellationToken);
}

/// <summary>lms.enrolment-created.v1 (sponsored) → link the enrolment to its seat allocation (ADR 0021, funding source = block) or to its programme seat; the learner is now "active".</summary>
internal sealed class EnrolmentCreatedConsumer(SponsorshipDbContext db, IClock clock) : IIntegrationEventHandler<EnrolmentCreated>
{
    public async Task HandleAsync(EnrolmentCreated @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (@event.FundingSource != FundingSources.Sponsored || @event.FundingSourceId is not { } programmeId)
        {
            return;
        }

        var allocation = await db.SeatAllocations.SingleOrDefaultAsync(a => a.BlockId == programmeId && a.LearnerId == @event.LearnerId && a.Status == SeatAllocationStatus.Enrolled, cancellationToken);
        if (allocation is not null)
        {
            if (allocation.EnrolmentId != @event.EnrolmentId)
            {
                allocation.LinkEnrolment(@event.EnrolmentId);
            }

            return;
        }

        var seat = await db.Seats.SingleOrDefaultAsync(s => s.ProgrammeId == programmeId && s.LearnerId == @event.LearnerId && s.Status == SeatStatus.Filled, cancellationToken);
        if (seat is null || seat.EnrolmentId == @event.EnrolmentId)
        {
            return;
        }

        seat.LinkEnrolment(@event.EnrolmentId);
        await Funnel.BumpAsync(db, envelope.TenantId, programmeId, "active", clock.UtcNow, cancellationToken);
    }
}

internal sealed class EnrolmentCompletedConsumer(SponsorshipDbContext db, IClock clock) : IIntegrationEventHandler<EnrolmentCompleted>
{
    public async Task HandleAsync(EnrolmentCompleted @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (@event.FundingSource == FundingSources.Sponsored && @event.FundingSourceId is { } programmeId)
        {
            await Funnel.BumpAsync(db, envelope.TenantId, programmeId, "completed", clock.UtcNow, cancellationToken);
        }
    }
}

internal sealed class CertificateIssuedConsumer(SponsorshipDbContext db, IClock clock) : IIntegrationEventHandler<CertificateIssued>
{
    public async Task HandleAsync(CertificateIssued @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var seat = await db.Seats.AsNoTracking().SingleOrDefaultAsync(s => s.EnrolmentId == @event.EnrolmentId, cancellationToken);
        if (seat is not null)
        {
            await Funnel.BumpAsync(db, envelope.TenantId, seat.ProgrammeId, "certified", clock.UtcNow, cancellationToken);
        }
    }
}

internal sealed class AttendanceRecordedConsumer(SponsorshipDbContext db) : IIntegrationEventHandler<AttendanceRecorded>
{
    public async Task HandleAsync(AttendanceRecorded @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (@event.EnrolmentId is not { } enrolmentId)
        {
            return;
        }

        var seat = await db.Seats.SingleOrDefaultAsync(s => s.EnrolmentId == enrolmentId, cancellationToken);
        if (seat is null || await db.Attendance.AnyAsync(a => a.EnrolmentId == enrolmentId && a.LiveSessionId == @event.LiveSessionId, cancellationToken))
        {
            return;
        }

        db.Attendance.Add(ProgrammeAttendance.Record(envelope.TenantId, seat.ProgrammeId, seat.Id, enrolmentId, @event.LiveSessionId, @event.JoinedAt, @event.LeftAt));
        seat.RecordAttendance();
    }
}

internal sealed class AttemptGradedConsumer(SponsorshipDbContext db) : IIntegrationEventHandler<AttemptGraded>
{
    public async Task HandleAsync(AttemptGraded @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var seat = await db.Seats.SingleOrDefaultAsync(s => s.EnrolmentId == @event.EnrolmentId, cancellationToken);
        seat?.RecordScore(@event.Score);
    }
}

/// <summary>
/// ADR 0021 §3: seats invited by phone or email convert when that contact gets an account. identity.user-registered.v1 carries the
/// phone blind index and, for email registrations, the lower-case email; identity.email-linked.v1 carries an email an existing account added.
/// </summary>
internal static class SeatInvites
{
    public static async Task ConvertAsync(SponsorshipDbContext db, SeatAllocator allocator, Guid userId, string? phoneHash, string? email, DateTime now, CancellationToken ct)
    {
        var hasPhone = !string.IsNullOrEmpty(phoneHash);
        var hasEmail = !string.IsNullOrEmpty(email);
        if (!hasPhone && !hasEmail)
        {
            return;
        }

        var invited = await db.SeatAllocations
            .Where(a => a.Status == SeatAllocationStatus.Invited && ((hasPhone && a.PhoneHash == phoneHash) || (hasEmail && a.Email == email)))
            .OrderBy(a => a.InvitedAt)
            .ToListAsync(ct);
        foreach (var allocation in invited)
        {
            var block = await db.SeatBlocks.SingleOrDefaultAsync(b => b.Id == allocation.BlockId, ct);
            if (block is null || block.Status != SeatBlockStatus.Active)
            {
                continue;
            }

            if (await db.SeatAllocations.AnyAsync(a => a.BlockId == block.Id && a.LearnerId == userId, ct))
            {
                continue; // one allocation per learner per block: a second invite for the same person stays invited
            }

            var enrolled = await allocator.EnrolAsync(block, allocation, userId, now, ct);
            if (enrolled.IsFailure)
            {
                throw new InvalidOperationException($"Seat invite {allocation.Id} could not be enrolled: {enrolled.Error.Code} {enrolled.Error.Message}");
            }
        }
    }
}

internal sealed class UserRegisteredConsumer(SponsorshipDbContext db, SeatAllocator allocator, IClock clock) : IIntegrationEventHandler<UserRegistered>
{
    public Task HandleAsync(UserRegistered @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
        => SeatInvites.ConvertAsync(db, allocator, @event.UserId, @event.PhoneHash, @event.Email, clock.UtcNow, cancellationToken);
}

internal sealed class EmailLinkedConsumer(SponsorshipDbContext db, SeatAllocator allocator, IClock clock) : IIntegrationEventHandler<EmailLinked>
{
    public Task HandleAsync(EmailLinked @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
        => SeatInvites.ConvertAsync(db, allocator, @event.UserId, null, @event.Email, clock.UtcNow, cancellationToken);
}
