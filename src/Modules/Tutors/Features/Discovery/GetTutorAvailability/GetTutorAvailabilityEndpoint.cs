using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;

namespace Plastoids.Modules.Tutors.Features.Discovery.GetTutorAvailability;

internal static class GetTutorAvailabilityEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/{tutorId:guid}/availability", async (Guid tutorId, DateOnly? from, int? days, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetTutorAvailabilityQuery(tutorId, from, days), ct)).ToHttpResult())
            .WithName("GetTutorAvailability")
            .WithSummary("Bookable one-hour UTC slots for the next N days (default 7), from weekly rules and exceptions.")
            .AllowAnonymous();
    }
}
