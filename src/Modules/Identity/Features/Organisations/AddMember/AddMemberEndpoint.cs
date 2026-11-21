using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;

namespace Plastoids.Modules.Identity.Features.Organisations.AddMember;

internal static class AddMemberEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{organisationId:guid}/members", async (Guid organisationId, AddMemberRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AddMemberCommand(organisationId, body.Email, body.Phone, body.Role), ct))
                    .ToCreatedResult(r => $"/api/v1/identity/organisations/{r.OrganisationId}/members/{r.UserId}"))
            .WithName("AddOrganisationMember")
            .WithSummary("Owner: add an existing user (email or phone) as owner | finance | reviewer | viewer.")
            .RequireAuthorization(IdentityPolicies.OrganisationOwner);
    }
}
