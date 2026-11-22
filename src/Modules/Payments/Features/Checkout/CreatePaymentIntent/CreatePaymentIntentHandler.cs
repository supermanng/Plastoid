using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.Modules.Payments.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payments.Features.Checkout.CreatePaymentIntent;

internal sealed class CreatePaymentIntentHandler(
    PaymentsDbContext db,
    IPaymentGateway gateway,
    IFeeRules fees,
    IPriceQuoteReader quotes,
    UnitCheckout units,
    IOptions<PaymentOptions> options,
    ICurrentUser currentUser,
    ICurrentTenant tenant,
    IClock clock) : ICommandHandler<CreatePaymentIntentCommand, CreatePaymentIntentResponse>
{
    public async Task<Result<CreatePaymentIntentResponse>> Handle(CreatePaymentIntentCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } payerId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        Money amount;
        Guid referenceId;
        Guid? payeeUserId;
        string description;
        if (request.Purpose == PaymentPurposes.Units)
        {
            return Error.Validation("purpose_invalid", "Buy units through POST /wallet/purchases.");
        }

        if (request.Purpose == PaymentPurposes.Course)
        {
            var quote = await quotes.GetAsync(request.QuoteId!, cancellationToken);
            if (quote is null || quote.ValidUntil < clock.UtcNow)
            {
                return Error.Validation("quote_expired", "The price quote has expired; reload the course page.");
            }

            amount = quote.Price;
            referenceId = quote.OfferingId;
            payeeUserId = quote.CourseOwnerUserId;
            description = $"Course: {quote.CourseTitle}";
        }
        else
        {
            amount = request.Amount!.Value;
            referenceId = request.ReferenceId!.Value;
            payeeUserId = request.PayeeUserId;
            description = request.Purpose == PaymentPurposes.Booking ? "Tutoring session" : "Sponsored programme co-payment";
        }

        if (amount.Currency != Money.Naira)
        {
            return Error.Validation("currency_unsupported", "Only NGN is supported at MVP.");
        }

        var fee = fees.Resolve(new SaleContext(request.Purpose, request.LearnerSource ?? LearnerSources.Platform));
        var intent = PaymentIntent.Create(tenant.TenantId, payerId, request.Purpose, referenceId, request.QuoteId, description, amount, payeeUserId, fee.CommissionBps, fee.RuleCode);

        if (request.PayWithUnits)
        {
            db.Intents.Add(intent);
            var paid = await units.PayAsync(intent, cancellationToken);
            if (paid.IsFailure)
            {
                return Result.Failure<CreatePaymentIntentResponse>(paid.Error);
            }

            return new CreatePaymentIntentResponse(intent.Id, intent.Gateway, intent.GatewayReference, null, amount, fee.CommissionBps, intent.Status.ToString().ToLowerInvariant(), intent.PaymentMethod, intent.UnitsCharged, intent.UnitRateKobo);
        }

        var callback = new Uri($"{options.Value.CallbackBaseUrl.TrimEnd('/')}/{intent.Id:N}");
        var email = $"{payerId:N}@{options.Value.PayerEmailFallbackDomain}";
        var session = await gateway.InitialiseCheckoutAsync(intent.Id, amount, email, callback, cancellationToken);
        if (session.IsFailure)
        {
            return Result.Failure<CreatePaymentIntentResponse>(session.Error);
        }

        intent.AttachCheckout(gateway.Name, session.Value.GatewayReference, session.Value.CheckoutUrl.ToString());
        db.Intents.Add(intent);
        return new CreatePaymentIntentResponse(intent.Id, gateway.Name, intent.GatewayReference, session.Value.CheckoutUrl, amount, fee.CommissionBps, intent.Status.ToString().ToLowerInvariant());
    }
}
