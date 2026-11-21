using FluentValidation;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Payments.Features.Checkout.VerifyPaymentIntent;

/// <summary>After the redirect: ask the gateway for the final status (handles a webhook that has not arrived yet).</summary>
public sealed record VerifyPaymentIntentCommand(Guid PaymentIntentId) : ICommand<VerifyPaymentIntentResponse>;

public sealed record VerifyPaymentIntentResponse(Guid PaymentIntentId, string Status, string GatewayStatus);

internal sealed class VerifyPaymentIntentValidator : AbstractValidator<VerifyPaymentIntentCommand>
{
    public VerifyPaymentIntentValidator()
    {
        RuleFor(x => x.PaymentIntentId).NotEmpty();
    }
}
