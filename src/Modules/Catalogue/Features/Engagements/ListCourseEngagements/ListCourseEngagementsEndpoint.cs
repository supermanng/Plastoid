using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;

namespace Plastoids.Modules.Catalogue.Features.Engagements.ListCourseEngagements;

internal static class ListCourseEngagementsEndpoint
{
    public static void Map(IEndpointRouteBuilder organisations)
    {
        organisations.MapGet("/{organisationId:guid}/courses/{courseId:guid}/engagements", async (Guid organisationId, Guid courseId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListCourseEngagementsQuery(organisationId, courseId), ct)).ToHttpResult())
            .WithName("ListCourseEngagements")
            .WithSummary("Organisation member: the tutors offered, teaching, declined or ended on this course.")
            .RequireAuthorization(IdentityPolicies.OrganisationMember);
    }
}
