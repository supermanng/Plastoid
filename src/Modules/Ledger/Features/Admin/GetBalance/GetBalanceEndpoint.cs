using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Ledger.Features.Admin.GetBalance;

internal static class GetBalanceEndpoint
{
    public static void Map(IEndpointRouteBuilder adminGroup)
    {
        adminGroup.MapGet("/accounts/{accountCode}/balance", async (string accountCode, Guid? ownerId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetBalanceQuery(accountCode, ownerId), ct)).ToHttpResult())
            .WithName("GetLedgerBalance")
            .WithSummary("Staff: natural balance of an account, optionally a sub-ledger (ownerId = tutor, programme or learner).")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
