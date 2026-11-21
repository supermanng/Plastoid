using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Identity.Features.Organisations.RegisterOrganisation;

internal static class RegisterOrganisationEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/", async (RegisterOrganisationCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToCreatedResult(r => $"/api/v1/identity/organisations/{r.OrganisationId}"))
            .WithName("RegisterOrganisation")
            .WithSummary("Submit an organisation for verification (CAC number + authorised signatory). Caller becomes owner.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
