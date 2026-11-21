using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Identity.Features.Me.GetMe;

internal static class GetMeEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetMeQuery(), ct)).ToHttpResult())
            .WithName("GetMe")
            .WithSummary("The signed-in user's profile, roles and organisations.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
