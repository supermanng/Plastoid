using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Identity.Features.Me.GetDataExport;

internal static class GetDataExportEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/data-export/{exportId:guid}", async (Guid exportId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetDataExportQuery(exportId), ct)).ToHttpResult())
            .WithName("GetDataExport")
            .WithSummary("Status of a data export and, once complete, one-hour download links.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
