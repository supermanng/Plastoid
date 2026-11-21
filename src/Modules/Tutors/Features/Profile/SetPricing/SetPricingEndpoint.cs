using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Tutors.Features.Profile.SetPricing;

internal static class SetPricingEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPut("/pricing", async (SetPricingCommand command, ISender sender, CancellationToken ct) => (await sender.Send(command, ct)).ToHttpResult())
            .WithName("SetPricing")
            .WithSummary("Hourly rate plus per-format session prices, in kobo.")
            .RequireAuthorization(Policies.Tutor);
    }
}
