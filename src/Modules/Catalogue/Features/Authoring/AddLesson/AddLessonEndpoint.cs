using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Authoring.AddLesson;

internal static class AddLessonEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{courseId:guid}/modules/{moduleId:guid}/lessons", async (Guid courseId, Guid moduleId, AddLessonRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AddLessonCommand(courseId, moduleId, body.Type, body.Title, body.Order, body.DurationMinutes, body.IsPreview, body.ContentJson), ct))
                    .ToCreatedResult(r => $"/api/v1/catalogue/lessons/{r.LessonId}"))
            .WithName("AddLesson")
            .WithSummary("Owner: add a video, doc, assignment or live lesson to a module.")
            .RequireAuthorization(Policies.Tutor);
    }
}
