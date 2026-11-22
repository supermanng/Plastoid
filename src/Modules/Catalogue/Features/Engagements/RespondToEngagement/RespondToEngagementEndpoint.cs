using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Engagements.RespondToEngagement;

internal static class RespondToEngagementEndpoint
{
    public static void Map(IEndpointRouteBuilder engagements)
    {
        engagements.MapPost("/{engagementId:guid}/respond", async (Guid engagementId, RespondToEngagementRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RespondToEngagementCommand(engagementId, body.Accept, body.Note), ct)).ToHttpResult())
            .WithName("RespondToEngagement")
            .WithSummary("Tutor: accept or decline an organisation's engagement offer.")
            .RequireAuthorization(Policies.Tutor);
    }
}
