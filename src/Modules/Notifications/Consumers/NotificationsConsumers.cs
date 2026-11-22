using System.Globalization;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Modules.Applications.Contracts;
using Plastoids.Modules.Booking.Contracts;
using Plastoids.Modules.Certificates.Contracts;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Notifications.Services;
using Plastoids.Modules.Payouts.Contracts;
using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Notifications.Consumers;

/// <summary>notifications.send-requested.v1 from any module: template key + recipient + data.</summary>
internal sealed class SendRequestedConsumer(NotificationDispatcher dispatcher) : IIntegrationEventHandler<NotificationRequested>
{
    public Task HandleAsync(NotificationRequested @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
        => dispatcher.DispatchAsync(@event.RecipientId, @event.TemplateKey, @event.Data, @event.ChannelPreference, envelope.Id, cancellationToken);
}

internal sealed class UserRegisteredConsumer(NotificationDispatcher dispatcher) : IIntegrationEventHandler<UserRegistered>
{
    public Task HandleAsync(UserRegistered @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
        => dispatcher.DispatchAsync(@event.UserId, "identity.user.registered", new Dictionary<string, string> { ["userId"] = @event.UserId.ToString("D"), ["role"] = @event.Role }, "preferred", envelope.Id, cancellationToken);
}

/// <summary>Both parties hear about a confirmed session; the data mirrors Booking's own send-requested so the two never duplicate.</summary>
internal sealed class BookingConfirmedConsumer(NotificationDispatcher dispatcher) : IIntegrationEventHandler<BookingConfirmed>
{
    public async Task HandleAsync(BookingConfirmed @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var data = new Dictionary<string, string> { ["bookingId"] = @event.BookingId.ToString("D"), ["slotStart"] = @event.SlotStart.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture), ["format"] = @event.Format };
        await dispatcher.DispatchAsync(@event.LearnerId, "booking.confirmed.learner", data, "preferred", envelope.Id, cancellationToken);
        await dispatcher.DispatchAsync(@event.TutorUserId, "booking.confirmed.tutor", data, "preferred", envelope.Id, cancellationToken);
    }
}

internal sealed class OfferSentConsumer(NotificationDispatcher dispatcher) : IIntegrationEventHandler<OfferSent>
{
    public Task HandleAsync(OfferSent @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
        => dispatcher.DispatchAsync(@event.LearnerId, "applications.offer.sent", new Dictionary<string, string> { ["applicationId"] = @event.ApplicationId.ToString("D"), ["expiresAt"] = @event.ExpiresAt.ToString("O", CultureInfo.InvariantCulture) }, "preferred", envelope.Id, cancellationToken);
}

internal sealed class OfferAcceptedConsumer(NotificationDispatcher dispatcher) : IIntegrationEventHandler<OfferAccepted>
{
    public Task HandleAsync(OfferAccepted @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
        => dispatcher.DispatchAsync(@event.LearnerId, "applications.offer.accepted", new Dictionary<string, string> { ["applicationId"] = @event.ApplicationId.ToString("D") }, "preferred", envelope.Id, cancellationToken);
}

internal sealed class CertificateIssuedConsumer(NotificationDispatcher dispatcher) : IIntegrationEventHandler<CertificateIssued>
{
    public Task HandleAsync(CertificateIssued @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
        => dispatcher.DispatchAsync(@event.LearnerId, "certificates.issued", new Dictionary<string, string> { ["certificateId"] = @event.CertificateId.ToString("D"), ["publicCode"] = @event.PublicCode }, "preferred", envelope.Id, cancellationToken);
}

/// <summary>Tutor payouts only: an organisation payout (ADR 0019) has no single recipient, so TutorUserId is Guid.Empty and nothing is sent.</summary>
internal sealed class PayoutSettledConsumer(NotificationDispatcher dispatcher) : IIntegrationEventHandler<PayoutSettled>
{
    public Task HandleAsync(PayoutSettled @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
        => @event.TutorUserId == Guid.Empty
            ? Task.CompletedTask
            : dispatcher.DispatchAsync(@event.TutorUserId, "payouts.payout.settled", new Dictionary<string, string> { ["payoutId"] = @event.PayoutId.ToString("D"), ["amount"] = @event.Amount.ToString(), ["bankRef"] = @event.BankRef }, "preferred", envelope.Id, cancellationToken);
}

internal sealed class EnrolmentCreatedConsumer(NotificationDispatcher dispatcher) : IIntegrationEventHandler<EnrolmentCreated>
{
    public Task HandleAsync(EnrolmentCreated @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
        => dispatcher.DispatchAsync(@event.LearnerId, "lms.enrolment.created", new Dictionary<string, string> { ["enrolmentId"] = @event.EnrolmentId.ToString("D") }, "preferred", envelope.Id, cancellationToken);
}
