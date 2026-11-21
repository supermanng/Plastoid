using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;

namespace Plastoids.Modules.Identity.Features.Organisations.ChangeMemberRole;

internal static class ChangeMemberRoleEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPut("/{organisationId:guid}/members/{userId:guid}/role", async (Guid organisationId, Guid userId, ChangeMemberRoleRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ChangeMemberRoleCommand(organisationId, userId, body.Role), ct)).ToHttpResult())
            .WithName("ChangeOrganisationMemberRole")
            .WithSummary("Owner: change a member's role; the last owner cannot be demoted.")
            .RequireAuthorization(IdentityPolicies.OrganisationOwner);
    }
}
