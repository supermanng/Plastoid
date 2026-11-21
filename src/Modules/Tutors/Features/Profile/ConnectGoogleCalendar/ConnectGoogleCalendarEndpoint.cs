using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Tutors.Features.Profile.ConnectGoogleCalendar;

internal static class ConnectGoogleCalendarEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/calendar/google", async (ConnectGoogleCalendarCommand command, ISender sender, CancellationToken ct) => (await sender.Send(command, ct)).ToHttpResult())
            .WithName("ConnectGoogleCalendar")
            .WithSummary("Exchange a Google OAuth code for calendar sync (adapter stub locally).")
            .RequireAuthorization(Policies.Tutor);
    }
}
