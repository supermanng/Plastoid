using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Second line of defence replacing PostgreSQL row-level security: refuses to write an entity whose
/// tenant differs from the current tenant unless running as a system context.
/// </summary>
public sealed class TenantWriteGuardInterceptor(ICurrentTenant tenant) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Guard(DbContext? context)
    {
        if (context is null || tenant.IsSystemContext)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && entry.Entity.TenantId != tenant.TenantId)
            {
                throw new CrossTenantWriteException(entry.Entity.GetType().Name, entry.Entity.TenantId, tenant.TenantId);
            }
        }
    }
}

public sealed class CrossTenantWriteException(string entity, Guid entityTenant, Guid currentTenant)
    : InvalidOperationException($"Refused to write {entity} belonging to tenant {entityTenant} while running as tenant {currentTenant}.")
{
    public string Entity { get; } = entity;
    public Guid EntityTenant { get; } = entityTenant;
    public Guid CurrentTenant { get; } = currentTenant;
}
