using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Tutors.Features.Profile.SetPayoutAccount;

internal static class SetPayoutAccountEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPut("/payout-account", async (SetPayoutAccountCommand command, ISender sender, CancellationToken ct) => (await sender.Send(command, ct)).ToHttpResult())
            .WithName("SetPayoutAccount")
            .WithSummary("Stage 5: resolve the bank account through the gateway and require the name to match the verified identity.")
            .RequireAuthorization(Policies.Tutor);
    }
}
