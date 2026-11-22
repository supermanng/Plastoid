using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Engagements.ListMyEngagements;

internal static class ListMyEngagementsEndpoint
{
    public static void Map(IEndpointRouteBuilder engagements)
    {
        engagements.MapGet("/mine", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListMyEngagementsQuery(), ct)).ToHttpResult())
            .WithName("ListMyEngagements")
            .WithSummary("Tutor: offers from organisations and the courses you teach for them.")
            .RequireAuthorization(Policies.Tutor);
    }
}
