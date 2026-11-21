using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;

namespace Plastoids.Modules.Identity.Features.Diagnostics.EmitSmokeEvent;

internal static class EmitSmokeEventEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/diagnostics/smoke", async (EmitSmokeEventCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.ToHttpResult(StatusCodes.Status202Accepted);
            })
            .WithName("EmitSmokeEvent")
            .WithSummary("Diagnostics: enqueue a smoke-test event through the outbox (non-production only).");
    }
}
