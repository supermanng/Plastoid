using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Plastoids.Infrastructure;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Payouts.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Payouts.Persistence;

public sealed class PayoutsDbContext(DbContextOptions<PayoutsDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "payouts";

    public DbSet<Earning> Earnings => Set<Earning>();
    public DbSet<DisputeHold> Holds => Set<DisputeHold>();
    public DbSet<PayoutBatch> Batches => Set<PayoutBatch>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<ProviderEvent> ProviderEvents => Set<ProviderEvent>();
    public DbSet<OrganisationPayoutAccount> OrganisationAccounts => Set<OrganisationPayoutAccount>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Earning>(b =>
        {
            b.ToTable("payouts_earnings");
            b.Ignore(x => x.PayableNet);
            b.Property(x => x.PayeeType).HasMaxLength(16);
            b.Property(x => x.SourceType).HasMaxLength(16);
            b.Property(x => x.SourceKey).HasMaxLength(120);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.HasIndex(x => x.SourceKey).IsUnique();
            b.HasIndex(x => new { x.PayeeType, x.PayeeId, x.Status });
            b.HasIndex(x => x.SourceId);
            b.HasIndex(x => x.ReferenceId);
            b.HasIndex(x => new { x.Status, x.ReleasesAt });
        });

        modelBuilder.Entity<DisputeHold>(b =>
        {
            b.ToTable("payouts_holds");
            b.Ignore(x => x.IsOpen);
            b.Property(x => x.PayeeType).HasMaxLength(16);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.Resolution).HasMaxLength(1000);
            b.HasIndex(x => new { x.PayeeType, x.PayeeId, x.ReleasedAt });
        });

        modelBuilder.Entity<PayoutBatch>(b =>
        {
            b.ToTable("payouts_batches");
            b.Property(x => x.Reference).HasMaxLength(40);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Provider).HasMaxLength(20);
            b.Property(x => x.RejectionReason).HasMaxLength(1000);
            b.HasIndex(x => x.Reference).IsUnique();
            b.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<Payout>(b =>
        {
            b.ToTable("payouts_payouts");
            b.Ignore(x => x.CountsAsPending);
            b.Ignore(x => x.TutorUserId);
            b.Property(x => x.PayeeType).HasMaxLength(16);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.BankCode).HasMaxLength(10);
            b.Property(x => x.AccountNumberMasked).HasMaxLength(20);
            b.Property(x => x.AccountName).HasMaxLength(160);
            b.Property(x => x.ProviderReference).HasMaxLength(80);
            b.Property(x => x.BankReference).HasMaxLength(80);
            b.Property(x => x.FailureReason).HasMaxLength(500);
            b.HasIndex(x => x.BatchId);
            b.HasIndex(x => new { x.PayeeType, x.PayeeId, x.Status });
            b.HasIndex(x => x.ProviderReference);
        });

        modelBuilder.Entity<OrganisationPayoutAccount>(b =>
        {
            b.ToTable("payouts_organisation_accounts");
            b.Property(x => x.BankCode).HasMaxLength(10);
            b.Property(x => x.AccountNumberCiphertext).HasMaxLength(512);
            b.Property(x => x.AccountNumberMasked).HasMaxLength(20);
            b.Property(x => x.AccountName).HasMaxLength(160);
            b.HasIndex(x => x.OrganisationId).IsUnique();
        });

        modelBuilder.Entity<ProviderEvent>(b =>
        {
            b.ToTable("payouts_provider_events");
            b.Property(x => x.Provider).HasMaxLength(20);
            b.Property(x => x.EventId).HasMaxLength(120);
            b.Property(x => x.EventType).HasMaxLength(40);
            b.Property(x => x.Reference).HasMaxLength(80);
            b.Property(x => x.Outcome).HasMaxLength(40);
            b.HasIndex(x => new { x.Provider, x.EventId }).IsUnique();
        });
    }
}

internal sealed class PayoutsDbContextFactory : IDesignTimeDbContextFactory<PayoutsDbContext>
{
    public PayoutsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PLASTOIDS_DESIGN_CONNECTION")
            ?? "Server=localhost;Port=3306;Database=plastoids;User=plastoids;Password=plastoids;";
        var options = new DbContextOptionsBuilder<PayoutsDbContext>()
            .UseMySql(InfrastructureServiceCollectionExtensions.EnsureGuidFormat(connection), ServerVersion.Parse("8.4.0-mysql"))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new PayoutsDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public Guid TenantId => Tenants.Marketplace;
        public bool IsSystemContext => true;
    }
}
