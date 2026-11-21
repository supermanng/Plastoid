using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Identity.Features.Me.UpdateGoals;

internal static class UpdateGoalsEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPut("/goals", async (UpdateGoalsCommand command, ISender sender, CancellationToken ct) => (await sender.Send(command, ct)).ToHttpResult())
            .WithName("UpdateGoals")
            .WithSummary("Set career goal and interests (blueprint learner journey step 2).")
            .RequireAuthorization(Policies.Authenticated);
    }
}
