using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plastoids.Infrastructure.Modules;
using Plastoids.Infrastructure.Persistence.Shared;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Infrastructure.Persistence;

/// <summary>Applies EF migrations in a fixed order: shared tables first, then each module in registration order.</summary>
public static class Migrator
{
    public static async Task MigrateAllAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await using var scope = provider.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrator");
        var registry = scope.ServiceProvider.GetRequiredService<ModuleRegistry>();

        var contexts = new List<DbContext> { scope.ServiceProvider.GetRequiredService<SharedDbContext>() };
        contexts.AddRange(registry.Modules.Select(m => (DbContext)scope.ServiceProvider.GetRequiredService(m.DbContextType)));

        foreach (var context in contexts)
        {
            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            logger.LogInformation("{Context}: {Count} pending migration(s)", context.GetType().Name, pending.Count);
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}
