using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;

namespace Plastoids.Modules.Payments.Services;

internal sealed class PaymentsAdminQueues(PaymentsDbContext db, IUnitOfWork unitOfWork) : IPaymentsAdminQueues
{
    public async Task<IReadOnlyList<PendingRefundItem>> ListPendingRefundsAsync(int limit, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        var rows = await db.Refunds.AsNoTracking().Where(r => r.Status == RefundStatus.PendingApproval).OrderBy(r => r.CreatedAt).Take(Math.Clamp(limit, 1, 200)).ToListAsync(cancellationToken);
        return rows.Select(r => new PendingRefundItem(r.Id, r.PaymentIntentId, r.Amount, r.Reason, r.RequestedBy, r.CreatedAt, r.Amount > Refund.FourEyesThreshold)).ToList();
    }
}
