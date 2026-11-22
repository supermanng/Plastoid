using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;

namespace Plastoids.Modules.Catalogue.Features.Engagements.OfferEngagement;

internal static class OfferEngagementEndpoint
{
    public static void Map(IEndpointRouteBuilder organisations)
    {
        organisations.MapPost("/{organisationId:guid}/courses/{courseId:guid}/engagements", async (Guid organisationId, Guid courseId, OfferEngagementRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new OfferEngagementCommand(organisationId, courseId, body.TutorId, body.Terms, body.Note), ct))
                    .ToCreatedResult(r => $"/api/v1/catalogue/organisations/{organisationId}/courses/{courseId}/engagements"))
            .WithName("OfferEngagement")
            .WithSummary("Organisation owner/author: offer a verified tutor terms (revenue_share bps or per_enrolment fee) to teach this course.")
            .RequireAuthorization(IdentityPolicies.OrganisationAuthor);
    }
}
