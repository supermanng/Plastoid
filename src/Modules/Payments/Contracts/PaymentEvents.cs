using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Payments.Contracts;

/// <summary>payments.payment-succeeded.v1 (build prompt §2.8). Purpose: course | booking | co_pay. ReferenceId: offering, booking or programme id.</summary>
[IntegrationEvent("payments", "payment-succeeded", 1)]
public sealed record PaymentSucceeded(Guid AggregateId, Guid PaymentIntentId, Guid PayerId, string Purpose, Guid ReferenceId, Money Amount, Guid? PayeeUserId, Money? PayeeNet = null) : IIntegrationEvent;

[IntegrationEvent("payments", "payment-failed", 1)]
public sealed record PaymentFailed(Guid AggregateId, Guid PaymentIntentId, string Reason) : IIntegrationEvent;

[IntegrationEvent("payments", "invoice-paid", 1)]
public sealed record InvoicePaid(Guid AggregateId, Guid InvoiceId, Guid OrganisationId, Guid? ProgrammeId, Money Amount) : IIntegrationEvent;

[IntegrationEvent("payments", "refund-issued", 1)]
public sealed record RefundIssued(Guid AggregateId, Guid RefundId, Guid OriginalPaymentIntentId, Money Amount) : IIntegrationEvent;
