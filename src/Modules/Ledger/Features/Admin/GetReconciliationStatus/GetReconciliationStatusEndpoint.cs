using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Ledger.Jobs;

namespace Plastoids.Modules.Ledger.Features.Admin.GetReconciliationStatus;

internal static class GetReconciliationStatusEndpoint
{
    public static void Map(IEndpointRouteBuilder adminGroup)
    {
        adminGroup.MapGet("/reconciliation", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetReconciliationStatusQuery(), ct)).ToHttpResult())
            .WithName("GetReconciliationStatus")
            .WithSummary("Staff: payouts_frozen flag and the last reconciliation / verification outcome.")
            .RequireAuthorization(Policies.StaffAdmin);

        adminGroup.MapPost("/reconciliation/run", async (ReconciliationJob job, ISender sender, CancellationToken ct) =>
            {
                var (assets, liabilities) = await job.RunAsync(ct);
                return Results.Ok(new { assetsKobo = assets, liabilitiesKobo = liabilities, shortfall = assets < liabilities });
            })
            .WithName("RunReconciliation")
            .WithSummary("Staff: run the daily reconciliation now.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
