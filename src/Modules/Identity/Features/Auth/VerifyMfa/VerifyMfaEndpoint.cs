using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;

namespace Plastoids.Modules.Identity.Features.Auth.VerifyMfa;

internal static class VerifyMfaEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/mfa/verify", async (VerifyMfaCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttpResult())
            .WithName("VerifyMfa")
            .WithSummary("Confirm MFA enrolment (signed in) or complete a login MFA challenge (mfaToken + code / recovery code).")
            .AllowAnonymous()
            .RequireRateLimiting("auth");
    }
}
