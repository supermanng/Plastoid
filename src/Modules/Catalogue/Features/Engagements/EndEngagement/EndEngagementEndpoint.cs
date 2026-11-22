using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Engagements.EndEngagement;

internal static class EndEngagementEndpoint
{
    public static void Map(IEndpointRouteBuilder engagements)
    {
        engagements.MapPost("/{engagementId:guid}/end", async (Guid engagementId, EndEngagementRequest? body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new EndEngagementCommand(engagementId, body?.Reason), ct)).ToHttpResult())
            .WithName("EndEngagement")
            .WithSummary("Course owner/author: end an active engagement; the tutor is notified.")
            .RequireAuthorization(Policies.Mfa); // the engagement's course must pass CourseAccess
    }
}
