using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Payments.Contracts;

/// <summary>
/// payments.payment-succeeded.v1 (build prompt §2.8). Purpose: course | booking | co_pay | units. ReferenceId: offering, booking, programme or wallet id.
/// PayeeType "tutor": PayeeUserId is paid PayeeNet. PayeeType "organisation" (ADR 0019): PayeeOrganisationId is paid PayeeNet and the engaged
/// tutor PayeeUserId (nullable) is paid TutorShare.
/// </summary>
[IntegrationEvent("payments", "payment-succeeded", 1)]
public sealed record PaymentSucceeded(Guid AggregateId, Guid PaymentIntentId, Guid PayerId, string Purpose, Guid ReferenceId, Money Amount, Guid? PayeeUserId, Money? PayeeNet = null,
    string? PayeeType = null, Guid? PayeeOrganisationId = null, Money? TutorShare = null) : IIntegrationEvent;

[IntegrationEvent("payments", "payment-failed", 1)]
public sealed record PaymentFailed(Guid AggregateId, Guid PaymentIntentId, string Reason) : IIntegrationEvent;

[IntegrationEvent("payments", "invoice-paid", 1)]
public sealed record InvoicePaid(Guid AggregateId, Guid InvoiceId, Guid OrganisationId, Guid? ProgrammeId, Money Amount) : IIntegrationEvent;

[IntegrationEvent("payments", "refund-issued", 1)]
public sealed record RefundIssued(Guid AggregateId, Guid RefundId, Guid OriginalPaymentIntentId, Money Amount) : IIntegrationEvent;
