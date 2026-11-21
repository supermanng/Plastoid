using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Identity.Features.Auth.EnableMfa;

internal static class EnableMfaEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/mfa/enable", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new EnableMfaCommand(), ct)).ToHttpResult())
            .WithName("EnableMfa")
            .WithSummary("Start TOTP enrolment; returns the secret, otpauth URI and one-time recovery codes.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
