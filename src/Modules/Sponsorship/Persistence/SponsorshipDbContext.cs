using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Plastoids.Infrastructure;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Sponsorship.Persistence;

public sealed class SponsorshipDbContext(DbContextOptions<SponsorshipDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "sponsorship";

    public DbSet<Programme> Programmes => Set<Programme>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<ProgrammeFunnel> Funnels => Set<ProgrammeFunnel>();
    public DbSet<ProgrammeMonth> Months => Set<ProgrammeMonth>();
    public DbSet<ProgrammeAttendance> Attendance => Set<ProgrammeAttendance>();
    public DbSet<FundingReceipt> Receipts => Set<FundingReceipt>();
    public DbSet<SeatBlock> SeatBlocks => Set<SeatBlock>();
    public DbSet<SeatAllocation> SeatAllocations => Set<SeatAllocation>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Programme>(b =>
        {
            b.ToTable("sponsorship_programmes");
            b.Ignore(x => x.IsEditable);
            b.Ignore(x => x.Quote);
            b.Ignore(x => x.IsFunded);
            b.Ignore(x => x.SeatsFundable);
            b.Ignore(x => x.DurationMonths);
            b.Ignore(x => x.CoPayPerSeat);
            b.Property(x => x.SponsorName).HasMaxLength(160);
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Objectives).HasMaxLength(2000);
            b.Property(x => x.CourseTitle).HasMaxLength(200);
            b.Property(x => x.EligibilityRulesJson).HasColumnType("json");
            b.Property(x => x.EntryAssessmentJson).HasColumnType("json");
            b.Property(x => x.ScoringRubricJson).HasColumnType("json");
            b.Property(x => x.PaymentPlanJson).HasColumnType("json");
            b.Property(x => x.SelectionMode).HasMaxLength(16);
            b.Property(x => x.UnusedFundsPolicy).HasMaxLength(16);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.ReviewStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.ReviewNotes).HasMaxLength(2000);
            b.HasIndex(x => x.OrganisationId);
            b.HasIndex(x => new { x.Status, x.ApplicationsCloseAt });
            b.HasIndex(x => x.InvoiceId);
        });

        modelBuilder.Entity<Seat>(b =>
        {
            b.ToTable("sponsorship_seats");
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.ReleaseReason).HasMaxLength(500);
            b.Property(x => x.LastScore).HasPrecision(5, 2);
            b.HasIndex(x => new { x.ProgrammeId, x.SeatNo }).IsUnique();
            b.HasIndex(x => new { x.ProgrammeId, x.Status });
            b.HasIndex(x => x.ApplicationId);
            b.HasIndex(x => x.EnrolmentId);
            b.HasIndex(x => x.LearnerId);
        });

        modelBuilder.Entity<ProgrammeFunnel>(b =>
        {
            b.ToTable("sponsorship_funnels");
            b.HasIndex(x => x.ProgrammeId).IsUnique();
        });

        modelBuilder.Entity<ProgrammeMonth>(b =>
        {
            b.ToTable("sponsorship_programme_months");
            b.HasIndex(x => new { x.ProgrammeId, x.MonthIndex }).IsUnique();
        });

        modelBuilder.Entity<ProgrammeAttendance>(b =>
        {
            b.ToTable("sponsorship_attendance");
            b.HasIndex(x => new { x.ProgrammeId, x.JoinedAt });
            b.HasIndex(x => new { x.EnrolmentId, x.LiveSessionId }).IsUnique();
        });

        modelBuilder.Entity<FundingReceipt>(b =>
        {
            b.ToTable("sponsorship_funding_receipts");
            b.HasIndex(x => x.ProgrammeId);
            b.HasIndex(x => x.InvoiceId).IsUnique();
        });

        modelBuilder.Entity<SeatBlock>(b =>
        {
            b.ToTable("sponsorship_seat_blocks");
            b.Ignore(x => x.Open);
            b.Ignore(x => x.TotalPrice);
            b.Property(x => x.SponsorName).HasMaxLength(160);
            b.Property(x => x.CohortName).HasMaxLength(160);
            b.Property(x => x.CourseTitle).HasMaxLength(200);
            b.Property(x => x.CourseSlug).HasMaxLength(200);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.HasIndex(x => new { x.OrganisationId, x.Status });
            b.HasIndex(x => x.CohortId);
            b.HasIndex(x => new { x.Status, x.EndsAt });
        });

        modelBuilder.Entity<SeatAllocation>(b =>
        {
            b.ToTable("sponsorship_seat_allocations");
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.DisplayName).HasMaxLength(120);
            b.Property(x => x.ContactKind).HasMaxLength(8);
            b.Property(x => x.PhoneHash).HasMaxLength(64);
            b.Property(x => x.Email).HasMaxLength(254);
            b.Property(x => x.ContactMasked).HasMaxLength(254);
            b.Property(x => x.PayeeType).HasMaxLength(16);
            b.HasIndex(x => new { x.BlockId, x.SeatNo }).IsUnique();
            b.HasIndex(x => new { x.BlockId, x.LearnerId });
            b.HasIndex(x => x.PhoneHash);
            b.HasIndex(x => x.Email);
            b.HasIndex(x => x.EnrolmentId);
        });
    }
}

internal sealed class SponsorshipDbContextFactory : IDesignTimeDbContextFactory<SponsorshipDbContext>
{
    public SponsorshipDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PLASTOIDS_DESIGN_CONNECTION")
            ?? "Server=localhost;Port=3306;Database=plastoids;User=plastoids;Password=plastoids;";
        var options = new DbContextOptionsBuilder<SponsorshipDbContext>()
            .UseMySql(InfrastructureServiceCollectionExtensions.EnsureGuidFormat(connection), ServerVersion.Parse("8.4.0-mysql"))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new SponsorshipDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public Guid TenantId => Plastoids.SharedKernel.Tenancy.Tenants.Marketplace;
        public bool IsSystemContext => true;
    }
}
