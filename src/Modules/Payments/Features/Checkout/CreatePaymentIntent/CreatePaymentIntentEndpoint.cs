using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Payments.Features.Checkout.CreatePaymentIntent;

internal static class CreatePaymentIntentEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/intents", async (CreatePaymentIntentRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new CreatePaymentIntentCommand(body.Purpose, body.QuoteId, body.ReferenceId, body.Amount, body.PayeeUserId, body.LearnerSource, key), ct))
                    .ToCreatedResult(r => $"/api/v1/payments/intents/{r.PaymentIntentId}");
            })
            .WithName("CreatePaymentIntent")
            .WithSummary("Start a hosted checkout (course via quoteId, or booking/co_pay with amount). Requires Idempotency-Key.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
