using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Plastoids.Infrastructure;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.BackOffice.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.BackOffice.Persistence;

public sealed class BackOfficeDbContext(DbContextOptions<BackOfficeDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "backoffice";

    public DbSet<MetricCounter> Metrics => Set<MetricCounter>();
    public DbSet<RiskAlert> Alerts => Set<RiskAlert>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MetricCounter>(b =>
        {
            b.ToTable("backoffice_metrics");
            b.Property(x => x.Key).HasMaxLength(40);
            b.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<RiskAlert>(b =>
        {
            b.ToTable("backoffice_risk_alerts");
            b.Ignore(x => x.IsOpen);
            b.Property(x => x.Kind).HasMaxLength(32);
            b.Property(x => x.Severity).HasMaxLength(16);
            b.Property(x => x.Message).HasMaxLength(500);
            b.Property(x => x.Resolution).HasMaxLength(1000);
            b.HasIndex(x => new { x.Kind, x.ResolvedAt });
            b.HasIndex(x => x.ReferenceId);
        });

        modelBuilder.Entity<FeatureFlag>(b =>
        {
            b.ToTable("backoffice_feature_flags");
            b.Property(x => x.Key).HasMaxLength(64);
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasIndex(x => x.Key).IsUnique();
        });
    }
}

internal sealed class BackOfficeDbContextFactory : IDesignTimeDbContextFactory<BackOfficeDbContext>
{
    public BackOfficeDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PLASTOIDS_DESIGN_CONNECTION")
            ?? "Server=localhost;Port=3306;Database=plastoids;User=plastoids;Password=plastoids;";
        var options = new DbContextOptionsBuilder<BackOfficeDbContext>()
            .UseMySql(InfrastructureServiceCollectionExtensions.EnsureGuidFormat(connection), ServerVersion.Parse("8.4.0-mysql"))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new BackOfficeDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public Guid TenantId => Tenants.Marketplace;
        public bool IsSystemContext => true;
    }
}
