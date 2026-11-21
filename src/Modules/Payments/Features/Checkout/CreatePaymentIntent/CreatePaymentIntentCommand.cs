using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Payments.Features.Checkout.CreatePaymentIntent;

/// <summary>
/// purpose=course: quoteId from GET /catalogue/courses/{slug} (price honoured 30 min). purpose=booking | co_pay: amount, referenceId
/// and payeeUserId supplied by the calling module/client. Money write → Idempotency-Key required.
/// </summary>
public sealed record CreatePaymentIntentCommand(string Purpose, string? QuoteId, Guid? ReferenceId, Money? Amount, Guid? PayeeUserId, string? LearnerSource, string IdempotencyKey)
    : ICommand<CreatePaymentIntentResponse>, IIdempotentCommand;

public sealed record CreatePaymentIntentRequest(string Purpose, string? QuoteId, Guid? ReferenceId, Money? Amount, Guid? PayeeUserId, string? LearnerSource);

public sealed record CreatePaymentIntentResponse(Guid PaymentIntentId, string Gateway, string GatewayReference, Uri CheckoutUrl, Money Amount, int CommissionBps, string Status);
