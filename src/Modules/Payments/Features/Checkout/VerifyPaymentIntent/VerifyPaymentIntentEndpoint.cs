using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Payments.Features.Checkout.VerifyPaymentIntent;

internal static class VerifyPaymentIntentEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/intents/{paymentIntentId:guid}/verify", async (Guid paymentIntentId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new VerifyPaymentIntentCommand(paymentIntentId), ct)).ToHttpResult())
            .WithName("VerifyPaymentIntent")
            .WithSummary("Payer: confirm with the gateway after returning from checkout (no-op once the webhook has landed).")
            .RequireAuthorization(Policies.Authenticated);
    }
}
