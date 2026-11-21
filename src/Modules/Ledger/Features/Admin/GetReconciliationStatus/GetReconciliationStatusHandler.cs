using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Ledger.Domain;
using Plastoids.Modules.Ledger.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Ledger.Features.Admin.GetReconciliationStatus;

internal sealed class GetReconciliationStatusHandler(LedgerDbContext db) : IQueryHandler<GetReconciliationStatusQuery, ReconciliationStatus>
{
    public async Task<Result<ReconciliationStatus>> Handle(GetReconciliationStatusQuery request, CancellationToken cancellationToken)
    {
        var c = await db.Controls.AsNoTracking().SingleOrDefaultAsync(x => x.Id == LedgerControl.SingletonId, cancellationToken);
        return c is null
            ? new ReconciliationStatus(false, null, null, null, null, null, 0)
            : new ReconciliationStatus(c.PayoutsFrozen, c.FreezeReason, c.LastReconciledAt, c.LastAssetsKobo, c.LastLiabilitiesKobo, c.LastVerifiedAt, c.LastUnbalancedEntries);
    }
}
