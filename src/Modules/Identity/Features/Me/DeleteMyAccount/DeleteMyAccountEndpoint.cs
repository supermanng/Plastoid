using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Identity.Features.Me.DeleteMyAccount;

internal static class DeleteMyAccountEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapDelete("/", async ([FromBody] DeleteMyAccountCommand command, ISender sender, CancellationToken ct) => (await sender.Send(command, ct)).ToHttpResult())
            .WithName("DeleteMyAccount")
            .WithSummary("Erase the account (NDPA). Body: { \"confirmation\": \"DELETE\" }.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
