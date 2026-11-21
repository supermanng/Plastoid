using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Publishing.ReviewCourse;

internal static class ReviewCourseEndpoint
{
    public static void Map(IEndpointRouteBuilder adminGroup)
    {
        adminGroup.MapPost("/courses/{courseId:guid}/review", async (Guid courseId, ReviewCourseRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ReviewCourseCommand(courseId, body.Decision, body.Note, key), ct)).ToHttpResult();
            })
            .WithName("ReviewCourse")
            .WithSummary("Staff: approve or reject a submitted course. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
