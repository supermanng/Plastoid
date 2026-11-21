using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Payouts.Contracts;
using Plastoids.Modules.Payouts.Persistence;

namespace Plastoids.Modules.Payouts.Services;

internal sealed class DisputeQueue(PayoutsDbContext db, IUnitOfWork unitOfWork) : IDisputeQueue
{
    public async Task<IReadOnlyList<DisputeHoldItem>> ListOpenAsync(DateTime utcNow, int limit, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        return await db.Holds.AsNoTracking().Where(h => h.ReleasedAt == null).OrderBy(h => h.ReleasesAt).Take(Math.Clamp(limit, 1, 200))
            .Select(h => new DisputeHoldItem(h.Id, h.TutorUserId, h.Reason, h.PlacedBy, h.PlacedAt, h.ReleasesAt, h.ReleasesAt < utcNow))
            .ToListAsync(cancellationToken);
    }
}
