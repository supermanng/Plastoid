using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Authoring.UpdateCourseOutline;

internal static class UpdateCourseOutlineEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPut("/{courseId:guid}/outline", async (Guid courseId, UpdateCourseOutlineRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateCourseOutlineCommand(courseId, body.Title, body.Summary, body.Level, body.Language, body.Category, body.Skills, body.Modules), ct)).ToHttpResult())
            .WithName("UpdateCourseOutline")
            .WithSummary("Owner: update metadata and the ordered module list (modules without an id are created; omitted ones are deleted).")
            .RequireAuthorization(Policies.Tutor);
    }
}
