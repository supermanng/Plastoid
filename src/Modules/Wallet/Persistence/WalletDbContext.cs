using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Plastoids.Infrastructure;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Wallet.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Wallet.Persistence;

public sealed class WalletDbContext(DbContextOptions<WalletDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "wallet";

    public DbSet<Domain.Wallet> Wallets => Set<Domain.Wallet>();
    public DbSet<UnitLot> Lots => Set<UnitLot>();
    public DbSet<WalletTransaction> Transactions => Set<WalletTransaction>();
    public DbSet<UnitPurchase> Purchases => Set<UnitPurchase>();
    public DbSet<UnitRate> Rates => Set<UnitRate>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Wallet>(b =>
        {
            b.ToTable("wallet_wallets");
            b.Property(x => x.OwnerType).HasMaxLength(16);
            b.HasIndex(x => new { x.OwnerType, x.OwnerId }).IsUnique();
        });

        modelBuilder.Entity<UnitLot>(b =>
        {
            b.ToTable("wallet_lots");
            b.Ignore(x => x.IsOpen);
            b.Property(x => x.Kind).HasMaxLength(16);
            b.Property(x => x.Reference).HasMaxLength(160);
            b.HasIndex(x => new { x.WalletId, x.UnitsRemaining, x.AcquiredAt });
            b.HasIndex(x => x.Reference).IsUnique();
        });

        modelBuilder.Entity<WalletTransaction>(b =>
        {
            b.ToTable("wallet_transactions");
            b.Property(x => x.Kind).HasMaxLength(16);
            b.Property(x => x.Reference).HasMaxLength(160);
            b.Property(x => x.Purpose).HasMaxLength(32);
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasIndex(x => x.Reference).IsUnique();
            b.HasIndex(x => new { x.WalletId, x.OccurredAt });
        });

        modelBuilder.Entity<UnitPurchase>(b =>
        {
            b.ToTable("wallet_purchases");
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.HasIndex(x => x.PaymentIntentId).IsUnique();
            b.HasIndex(x => new { x.WalletId, x.Status });
        });

        modelBuilder.Entity<UnitRate>(b =>
        {
            b.ToTable("wallet_unit_rates");
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.Rationale).HasMaxLength(1000);
            b.Property(x => x.DecisionNote).HasMaxLength(1000);
            b.HasIndex(x => new { x.Status, x.EffectiveFrom });
            b.HasIndex(x => x.Version).IsUnique();
        });
    }
}

internal sealed class WalletDbContextFactory : IDesignTimeDbContextFactory<WalletDbContext>
{
    public WalletDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Plastoids") ?? "Server=localhost;Port=3306;Database=plastoids;User=plastoids;Password=plastoids";
        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseMySql(InfrastructureServiceCollectionExtensions.EnsureGuidFormat(connection), ServerVersion.Parse("8.4.0-mysql"))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new WalletDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public Guid TenantId => Tenants.Marketplace;
        public bool IsSystemContext => true;
    }
}
