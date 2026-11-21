using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Infrastructure.Persistence.Interceptors;

/// <summary>Sets created_at / updated_at and bumps row_version (MySQL has no rowversion type).</summary>
public sealed class AuditableEntityInterceptor(IClock clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = clock.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(Entity.CreatedAt)).CurrentValue = now;
                    entry.Property(nameof(Entity.UpdatedAt)).CurrentValue = now;
                    entry.Property(nameof(Entity.RowVersion)).CurrentValue = 1UL;
                    break;
                case EntityState.Modified:
                    entry.Property(nameof(Entity.UpdatedAt)).CurrentValue = now;
                    var version = entry.Property(nameof(Entity.RowVersion));
                    var original = (ulong)(version.OriginalValue ?? 0UL);
                    version.CurrentValue = original + 1;
                    break;
            }
        }
    }
}
