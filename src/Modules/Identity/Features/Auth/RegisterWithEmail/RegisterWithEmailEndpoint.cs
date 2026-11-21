using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Identity.Features.Auth.RegisterWithEmail;

internal static class RegisterWithEmailEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/register-email", async (RegisterWithEmailCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttpResult())
            .WithName("RegisterWithEmail")
            .WithSummary("Add email + password sign-in to the signed-in phone account.")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("auth");
    }
}
