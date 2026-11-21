using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Plastoids.Infrastructure;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Applications.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Applications.Persistence;

public sealed class ApplicationsDbContext(DbContextOptions<ApplicationsDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "applications";

    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<EntryAttempt> Attempts => Set<EntryAttempt>();
    public DbSet<Appeal> Appeals => Set<Appeal>();
    public DbSet<ShortlistApproval> ShortlistApprovals => Set<ShortlistApproval>();
    public DbSet<ProgrammeRef> Programmes => Set<ProgrammeRef>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Application>(b =>
        {
            b.ToTable("applications_applications");
            b.Ignore(x => x.IsFlagged);
            b.Ignore(x => x.HoldsSeat);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.AnswersJson).HasColumnType("json");
            b.Property(x => x.NinHash).HasMaxLength(64);
            b.Property(x => x.PhoneHash).HasMaxLength(64);
            b.Property(x => x.FlagReason).HasMaxLength(64);
            b.Property(x => x.LastDecision).HasMaxLength(16);
            b.Property(x => x.AssessmentScore).HasPrecision(5, 2);
            b.Property(x => x.RubricScore).HasPrecision(5, 2);
            b.Property(x => x.TotalScore).HasPrecision(5, 2);
            b.HasIndex(x => new { x.ProgrammeId, x.LearnerId });
            b.HasIndex(x => new { x.ProgrammeId, x.Status });
            b.HasIndex(x => new { x.ProgrammeId, x.NinHash });
            b.HasIndex(x => new { x.ProgrammeId, x.PhoneHash });
            b.HasIndex(x => x.LearnerId);
            b.HasIndex(x => x.CoPayIntentId);
            b.HasIndex(x => x.SeatId);
        });

        modelBuilder.Entity<Decision>(b =>
        {
            b.ToTable("applications_decisions");
            b.Property(x => x.Kind).HasMaxLength(16);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.Source).HasMaxLength(16);
            b.HasIndex(x => new { x.ApplicationId, x.DecidedAt });
            b.HasIndex(x => x.ProgrammeId);
        });

        modelBuilder.Entity<EntryAttempt>(b =>
        {
            b.ToTable("applications_entry_attempts");
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.AnswersJson).HasColumnType("json");
            b.Property(x => x.Score).HasPrecision(5, 2);
            b.Property(x => x.MaxScore).HasPrecision(5, 2);
            b.HasIndex(x => new { x.ApplicationId, x.Status });
        });

        modelBuilder.Entity<Appeal>(b =>
        {
            b.ToTable("applications_appeals");
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.Reason).HasMaxLength(2000);
            b.Property(x => x.Resolution).HasMaxLength(2000);
            b.HasIndex(x => new { x.ApplicationId, x.Status });
        });

        modelBuilder.Entity<ShortlistApproval>(b =>
        {
            b.ToTable("applications_shortlist_approvals");
            b.HasIndex(x => x.ProgrammeId).IsUnique();
        });

        modelBuilder.Entity<ProgrammeRef>(b =>
        {
            b.ToTable("applications_programmes");
            b.HasIndex(x => x.ProgrammeId).IsUnique();
        });
    }
}

internal sealed class ApplicationsDbContextFactory : IDesignTimeDbContextFactory<ApplicationsDbContext>
{
    public ApplicationsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PLASTOIDS_DESIGN_CONNECTION")
            ?? "Server=localhost;Port=3306;Database=plastoids;User=plastoids;Password=plastoids;";
        var options = new DbContextOptionsBuilder<ApplicationsDbContext>()
            .UseMySql(InfrastructureServiceCollectionExtensions.EnsureGuidFormat(connection), ServerVersion.Parse("8.4.0-mysql"))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new ApplicationsDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public Guid TenantId => Tenants.Marketplace;
        public bool IsSystemContext => true;
    }
}
