using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Payments.Features.Checkout.HandleGatewayWebhook;

internal static class HandleGatewayWebhookEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        // Paystack sends x-paystack-signature; the fake gateway uses the same header.
        group.MapPost("/webhooks/{gateway}", async (string gateway, HttpRequest request, IPaymentGateway provider, ISender sender, CancellationToken ct) =>
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync(ct);
                var signature = request.Headers["x-paystack-signature"].FirstOrDefault() ?? request.Headers["X-Gateway-Signature"].FirstOrDefault() ?? string.Empty;
                if (!string.Equals(gateway, provider.Name, StringComparison.OrdinalIgnoreCase) || !provider.VerifyWebhookSignature(body, signature))
                {
                    return ResultHttp.Problem(Plastoids.SharedKernel.Results.Error.Unauthorized("webhook_signature_invalid", "Webhook signature is missing or invalid."));
                }

                // Webhooks carry no user; run as the system actor in the marketplace tenant.
                using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
                {
                    return (await sender.Send(new HandleGatewayWebhookCommand(provider.Name, body), ct)).ToHttpResult();
                }
            })
            .WithName("PaymentGatewayWebhook")
            .WithSummary("Gateway webhook (HMAC-verified, idempotent on event id): charge.success/failed and virtual-account credits.")
            .AllowAnonymous();
    }
}
