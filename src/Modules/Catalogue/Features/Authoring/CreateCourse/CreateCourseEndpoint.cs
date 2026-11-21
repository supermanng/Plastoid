using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Authoring.CreateCourse;

internal static class CreateCourseEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/", async (CreateCourseCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToCreatedResult(r => $"/api/v1/catalogue/courses/{r.CourseId}"))
            .WithName("CreateCourse")
            .WithSummary("Tutor: start a draft course (title, summary, level, language, category, skills).")
            .RequireAuthorization(Policies.Tutor);
    }
}
