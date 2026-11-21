using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Identity.Features.Organisations.VerifyOrganisation;

internal static class VerifyOrganisationEndpoint
{
    public static void Map(IEndpointRouteBuilder adminGroup)
    {
        adminGroup.MapPost("/organisations/{organisationId:guid}/verification", async (Guid organisationId, VerifyOrganisationRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new VerifyOrganisationCommand(organisationId, body.Decision, body.Reason, key), ct)).ToHttpResult();
            })
            .WithName("VerifyOrganisation")
            .WithSummary("Staff: record the verification decision (verified | rejected). Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
