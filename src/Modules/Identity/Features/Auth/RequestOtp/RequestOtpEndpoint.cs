using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;

namespace Plastoids.Modules.Identity.Features.Auth.RequestOtp;

internal static class RequestOtpEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/otp/request", async (RequestOtpCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttpResult(StatusCodes.Status202Accepted))
            .WithName("RequestOtp")
            .WithSummary("Send a one-time code by SMS (5 per phone per hour).")
            .AllowAnonymous()
            .RequireRateLimiting("auth");
    }
}
