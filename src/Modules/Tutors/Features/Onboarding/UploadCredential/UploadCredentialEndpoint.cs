using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Tutors.Features.Onboarding.UploadCredential;

internal static class UploadCredentialEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/credentials", async (UploadCredentialCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToCreatedResult(r => $"/api/v1/tutors/me/credentials/{r.CredentialId}"))
            .WithName("UploadCredential")
            .WithSummary("Stage 2: register a degree/certificate and get a presigned upload URL; queued for reviewer approval (3 working days).")
            .RequireAuthorization(Policies.Tutor);
    }
}
