using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.BackOffice.Contracts;
using Plastoids.Modules.BackOffice.Domain;
using Plastoids.Modules.BackOffice.Persistence;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.BackOffice.Services;

/// <summary>Bumps the dashboard projection; tracked-first lookup so several events in one transaction share a row.</summary>
internal static class MetricProjection
{
    public static async Task BumpAsync(BackOfficeDbContext db, Guid tenantId, string key, Money? amount, DateTime now, CancellationToken ct)
    {
        var counter = db.Metrics.Local.SingleOrDefault(m => m.Key == key) ?? await db.Metrics.SingleOrDefaultAsync(m => m.Key == key, ct);
        if (counter is null)
        {
            counter = MetricCounter.Start(tenantId, key);
            db.Metrics.Add(counter);
        }

        counter.Bump(amount, now);
    }
}

internal sealed class FeatureFlags(BackOfficeDbContext db, IUnitOfWork unitOfWork) : IFeatureFlags
{
    public async Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        return await db.FeatureFlags.AsNoTracking().Where(f => f.Key == key).Select(f => f.Enabled).SingleOrDefaultAsync(cancellationToken);
    }
}
