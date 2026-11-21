using FluentValidation;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Payments.Features.Checkout.HandleGatewayWebhook;

/// <summary>Signature already verified by the endpoint. Dedup on (gateway, eventId); safe when it arrives before the redirect or twice.</summary>
public sealed record HandleGatewayWebhookCommand(string Gateway, string Payload) : ICommand<HandleGatewayWebhookResponse>;

public sealed record HandleGatewayWebhookResponse(string EventType, string Reference, string Outcome);

internal sealed class HandleGatewayWebhookValidator : AbstractValidator<HandleGatewayWebhookCommand>
{
    public HandleGatewayWebhookValidator()
    {
        RuleFor(x => x.Gateway).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Payload).NotEmpty().MaximumLength(64_000);
    }
}
