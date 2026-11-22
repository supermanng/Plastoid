using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Payments.Features.Checkout.CreatePaymentIntent;

/// <summary>
/// purpose=course: quoteId from GET /catalogue/courses/{slug} (price honoured 30 min). purpose=booking | co_pay: amount, referenceId
/// and payeeUserId supplied by the calling module/client. Money write → Idempotency-Key required.
/// </summary>
public sealed record CreatePaymentIntentCommand(string Purpose, string? QuoteId, Guid? ReferenceId, Money? Amount, Guid? PayeeUserId, string? LearnerSource, bool PayWithUnits, string IdempotencyKey)
    : ICommand<CreatePaymentIntentResponse>, IIdempotentCommand;

public sealed record CreatePaymentIntentRequest(string Purpose, string? QuoteId, Guid? ReferenceId, Money? Amount, Guid? PayeeUserId, string? LearnerSource, bool PayWithUnits = false);

/// <summary>
/// payWithUnits=true settles from the signed-in user's unit wallet at once: Status "succeeded", CheckoutUrl null, UnitsCharged set (ADR 0018).
/// PayeeType "organisation" (ADR 0019): the course belongs to PayeeOrganisationId and TutorShare goes to the engaged tutor.
/// </summary>
public sealed record CreatePaymentIntentResponse(Guid PaymentIntentId, string Gateway, string GatewayReference, Uri? CheckoutUrl, Money Amount, int CommissionBps, string Status, string PaymentMethod = "gateway", long UnitsCharged = 0, long UnitRateKobo = 0,
    string PayeeType = "tutor", Guid? PayeeOrganisationId = null, Money? TutorShare = null);
