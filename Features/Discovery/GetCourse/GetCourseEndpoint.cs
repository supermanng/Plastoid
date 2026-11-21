using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;

namespace Plastoids.Modules.Catalogue.Features.Discovery.GetCourse;

internal static class GetCourseEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/{slug}", async (string slug, ISender sender, CancellationToken ct) => (await sender.Send(new GetCourseQuery(slug), ct)).ToHttpResult())
            .WithName("GetCourse")
            .WithSummary("Course detail by slug with outline, offerings (each with a 30-minute price quote) and recent reviews.")
            .AllowAnonymous();
    }
}
