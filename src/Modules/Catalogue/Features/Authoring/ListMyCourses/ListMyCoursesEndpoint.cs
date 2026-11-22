using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Authoring.ListMyCourses;

internal static class ListMyCoursesEndpoint
{
    public static void Map(IEndpointRouteBuilder courses)
    {
        // Mapped before GetCourse so the literal segment is never read as a slug.
        courses.MapGet("/mine", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListMyCoursesQuery(), ct)).ToHttpResult())
            .WithName("ListMyCourses")
            .WithSummary("Tutor: the courses you created, with offering and engagement counts.")
            .RequireAuthorization(Policies.Tutor);
    }
}
