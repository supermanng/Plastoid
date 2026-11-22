using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;

namespace Plastoids.Modules.Catalogue.Features.Authoring.ListOrganisationCourses;

internal static class ListOrganisationCoursesEndpoint
{
    public static void Map(IEndpointRouteBuilder organisations)
    {
        organisations.MapGet("/{organisationId:guid}/courses", async (Guid organisationId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListOrganisationCoursesQuery(organisationId), ct)).ToHttpResult())
            .WithName("ListOrganisationCourses")
            .WithSummary("Organisation member: the organisation's courses with offering and active-engagement counts.")
            .RequireAuthorization(IdentityPolicies.OrganisationMember);
    }
}
