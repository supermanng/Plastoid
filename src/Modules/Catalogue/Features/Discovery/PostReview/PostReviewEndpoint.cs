using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Discovery.PostReview;

internal static class PostReviewEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{courseId:guid}/reviews", async (Guid courseId, PostReviewRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new PostReviewCommand(courseId, body.EnrolmentId, body.Rating, body.Body), ct)).ToCreatedResult(r => $"/api/v1/catalogue/reviews/{r.ReviewId}"))
            .WithName("PostCourseReview")
            .WithSummary("Learner: review a course (verified enrolment with ≥30% progress).")
            .RequireAuthorization(Policies.Authenticated);
    }
}
