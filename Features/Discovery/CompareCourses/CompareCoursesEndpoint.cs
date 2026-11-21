using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;

namespace Plastoids.Modules.Catalogue.Features.Discovery.CompareCourses;

internal static class CompareCoursesEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/compare", async (string ids, ISender sender, CancellationToken ct) =>
            {
                var parsed = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
                return (await sender.Send(new CompareCoursesQuery(parsed), ct)).ToHttpResult();
            })
            .WithName("CompareCourses")
            .WithSummary("Compare up to 3 published courses: ?ids=a,b,c")
            .AllowAnonymous();
    }
}
