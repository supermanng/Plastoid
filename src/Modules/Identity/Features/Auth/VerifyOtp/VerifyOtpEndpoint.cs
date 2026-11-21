using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;

namespace Plastoids.Modules.Identity.Features.Auth.VerifyOtp;

internal static class VerifyOtpEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/otp/verify", async (VerifyOtpCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.IsSuccess && result.Value.IsNewAccount
                    ? Results.Json(result.Value, statusCode: StatusCodes.Status201Created)
                    : result.ToHttpResult();
            })
            .WithName("VerifyOtp")
            .WithSummary("Verify the SMS code; signs in, or creates the account (201) for a new phone.")
            .AllowAnonymous()
            .RequireRateLimiting("auth");
    }
}
