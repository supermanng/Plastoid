using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Tutors.Features.Onboarding.SubmitIdentityCheck;

internal static class SubmitIdentityCheckEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/identity-check", async (SubmitIdentityCheckCommand command, ISender sender, CancellationToken ct) => (await sender.Send(command, ct)).ToHttpResult())
            .WithName("SubmitIdentityCheck")
            .WithSummary("Stage 1: NIN + BVN + liveness through the KYC provider (synchronous).")
            .RequireAuthorization(Policies.Tutor)
            .RequireRateLimiting("auth");
    }
}
