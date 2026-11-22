using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Authoring.GetAuthoringView;

internal static class GetAuthoringViewEndpoint
{
    public static void Map(IEndpointRouteBuilder courses)
    {
        courses.MapGet("/{courseId:guid}/authoring", async (Guid courseId, ISender sender, CancellationToken ct) => (await sender.Send(new GetAuthoringViewQuery(courseId), ct)).ToHttpResult())
            .WithName("GetCourseAuthoringView")
            .WithSummary("Owner: the draft course (fields, status, modules with lessons, offerings) for the authoring screens.")
            .RequireAuthorization(Policies.Mfa); // ownership (tutor, organisation owner/author, staff) is decided by CourseAccess (ADR 0019 §2)
    }
}
