using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;

namespace Plastoids.Modules.Catalogue.Features.Discovery.SearchCourses;

internal static class SearchCoursesEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (string? q, string? skill, string? level, string? funding, string? format, long? maxPrice, string? lang, string? category, string? cursor, int? limit, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SearchCoursesQuery(q, skill, level, funding, format, maxPrice, lang, category, cursor, limit), ct)).ToHttpResult())
            .WithName("SearchCourses")
            .WithSummary("Search published courses: q (full-text), skill, level, funding=sponsored, format, maxPrice (kobo), lang, category; zero results include suggestions.")
            .AllowAnonymous();
    }
}
