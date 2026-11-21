using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Tutors.Features.Profile.SetAvailability;

internal static class SetAvailabilityEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPut("/availability", async (SetAvailabilityCommand command, ISender sender, CancellationToken ct) => (await sender.Send(command, ct)).ToHttpResult())
            .WithName("SetAvailability")
            .WithSummary("Weekly windows plus dated exceptions in the tutor's time zone (story C3).")
            .RequireAuthorization(Policies.Tutor);
    }
}
