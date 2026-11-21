using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Ledger.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Ledger.Features.Admin.GetBalance;

internal sealed class GetBalanceHandler(LedgerDbContext db, ILedgerService ledger) : IQueryHandler<GetBalanceQuery, BalanceResponse>
{
    public async Task<Result<BalanceResponse>> Handle(GetBalanceQuery request, CancellationToken cancellationToken)
    {
        if (!await db.Accounts.AsNoTracking().AnyAsync(a => a.Code == request.AccountCode, cancellationToken))
        {
            return Error.NotFound("ledger_unknown_account", "No such account code.");
        }

        return new BalanceResponse(request.AccountCode, request.OwnerId, await ledger.GetBalanceAsync(request.AccountCode, request.OwnerId, cancellationToken));
    }
}
