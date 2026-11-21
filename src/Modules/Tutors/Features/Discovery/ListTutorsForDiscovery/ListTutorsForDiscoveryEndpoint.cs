using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;

namespace Plastoids.Modules.Tutors.Features.Discovery.ListTutorsForDiscovery;

internal static class ListTutorsForDiscoveryEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (string? q, string? skill, long? maxRate, string? lang, string? format, string? cursor, int? limit, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListTutorsForDiscoveryQuery(q, skill, maxRate, lang, format, cursor, limit), ct)).ToHttpResult())
            .WithName("ListTutorsForDiscovery")
            .WithSummary("Find verified tutors: q (full-text), skill, maxRate (kobo), lang, format, cursor paging.")
            .AllowAnonymous();
    }
}
