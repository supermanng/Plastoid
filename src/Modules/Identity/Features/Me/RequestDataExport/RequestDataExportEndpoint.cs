using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Identity.Features.Me.RequestDataExport;

internal static class RequestDataExportEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/data-export", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new RequestDataExportCommand(), ct)).ToHttpResult(StatusCodes.Status202Accepted))
            .WithName("RequestDataExport")
            .WithSummary("NDPA data export (JSON + PDF) produced by a background job within 24 h.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
