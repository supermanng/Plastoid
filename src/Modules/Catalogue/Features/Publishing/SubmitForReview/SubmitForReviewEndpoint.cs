using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Publishing.SubmitForReview;

internal static class SubmitForReviewEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{courseId:guid}/submit", async (Guid courseId, ISender sender, CancellationToken ct) => (await sender.Send(new SubmitForReviewCommand(courseId), ct)).ToHttpResult())
            .WithName("SubmitCourseForReview")
            .WithSummary("Owner: run automated checks and queue the course for content review (human for the first 3 courses, sampled after).")
            .RequireAuthorization(Policies.Tutor);
    }
}
