using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Publishing.PublishCourse;

internal static class PublishCourseEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{courseId:guid}/publish", async (Guid courseId, ISender sender, CancellationToken ct) => (await sender.Send(new PublishCourseCommand(courseId), ct)).ToHttpResult())
            .WithName("PublishCourse")
            .WithSummary("Owner: publish an approved course as a new immutable version; emits catalogue.course-published.v1.")
            .RequireAuthorization(Policies.Mfa); // ownership (tutor, organisation owner/author, staff) is decided by CourseAccess (ADR 0019 §2)
    }
}
