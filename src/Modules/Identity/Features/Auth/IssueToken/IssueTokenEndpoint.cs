using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;

namespace Plastoids.Modules.Identity.Features.Auth.IssueToken;

internal static class IssueTokenEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/token", async (IssueTokenCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttpResult())
            .WithName("IssueToken")
            .WithSummary("Issue tokens: grantType password (email+password), refresh_token, or google (idToken).")
            .AllowAnonymous()
            .RequireRateLimiting("auth");
    }
}
