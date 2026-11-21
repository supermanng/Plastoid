using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Tutors.Features.Onboarding.ReviewCredential;

internal static class ReviewCredentialEndpoint
{
    public static void Map(IEndpointRouteBuilder adminGroup)
    {
        adminGroup.MapPost("/credentials/{credentialId:guid}/review", async (Guid credentialId, ReviewCredentialRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ReviewCredentialCommand(credentialId, body.Decision, body.Note, key), ct)).ToHttpResult();
            })
            .WithName("ReviewCredential")
            .WithSummary("Staff reviewer: approve or reject a credential. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
