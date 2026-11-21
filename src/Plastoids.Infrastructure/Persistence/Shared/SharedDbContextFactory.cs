using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Infrastructure.Persistence.Shared;

internal sealed class SharedDbContextFactory : IDesignTimeDbContextFactory<SharedDbContext>
{
    public SharedDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PLASTOIDS_DESIGN_CONNECTION")
            ?? "Server=localhost;Port=3306;Database=plastoids;User=plastoids;Password=plastoids;";
        var options = new DbContextOptionsBuilder<SharedDbContext>()
            .UseMySql(InfrastructureServiceCollectionExtensions.EnsureGuidFormat(connection), ServerVersion.Parse("8.4.0-mysql"))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new SharedDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public Guid TenantId => Tenants.Marketplace;
        public bool IsSystemContext => true;
    }
}
