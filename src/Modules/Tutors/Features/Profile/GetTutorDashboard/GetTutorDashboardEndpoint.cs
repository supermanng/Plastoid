using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Tutors.Features.Profile.GetTutorDashboard;

internal static class GetTutorDashboardEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetTutorDashboardQuery(), ct)).ToHttpResult())
            .WithName("GetTutorDashboard")
            .WithSummary("Verification status per stage, credentials, availability, pricing, payout and quality metrics.")
            .RequireAuthorization(Policies.Tutor);
    }
}
