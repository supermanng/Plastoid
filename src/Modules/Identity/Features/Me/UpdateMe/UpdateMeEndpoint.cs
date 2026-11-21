using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Identity.Features.Me.UpdateMe;

internal static class UpdateMeEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPatch("/", async (UpdateMeCommand command, ISender sender, CancellationToken ct) => (await sender.Send(command, ct)).ToHttpResult())
            .WithName("UpdateMe")
            .WithSummary("Update display name and/or email.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
