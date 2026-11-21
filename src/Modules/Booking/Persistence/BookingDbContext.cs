using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Plastoids.Infrastructure;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Booking.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Booking.Persistence;

public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "booking";

    public DbSet<Domain.Booking> Bookings => Set<Domain.Booking>();
    public DbSet<SessionPackage> Packages => Set<SessionPackage>();
    public DbSet<TutorStrike> Strikes => Set<TutorStrike>();
    public DbSet<EarningHold> Holds => Set<EarningHold>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Booking>(b =>
        {
            b.ToTable("booking_bookings");
            b.Ignore(x => x.RefundAmount);
            b.Ignore(x => x.IsLive);
            b.Property(x => x.Format).HasMaxLength(16);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.SlotKey).HasMaxLength(64);
            b.HasIndex(x => x.SlotKey).IsUnique();
            b.Property(x => x.CancelledBy).HasMaxLength(16);
            b.Property(x => x.CancellationReason).HasMaxLength(500);
            b.Property(x => x.Note).HasMaxLength(500);
            b.HasIndex(x => new { x.LearnerId, x.SlotStart });
            b.HasIndex(x => new { x.TutorId, x.SlotStart });
            b.HasIndex(x => new { x.Status, x.SlotStart });
        });
        modelBuilder.Entity<SessionPackage>(b =>
        {
            b.ToTable("booking_packages");
            b.Ignore(x => x.SessionsRemaining);
            b.Ignore(x => x.EffectivePricePerSession);
            b.Property(x => x.Format).HasMaxLength(16);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.HasIndex(x => new { x.LearnerId, x.TutorId });
        });
        modelBuilder.Entity<TutorStrike>(b =>
        {
            b.ToTable("booking_tutor_strikes");
            b.Property(x => x.Reason).HasMaxLength(300);
            b.HasIndex(x => new { x.TutorId, x.OccurredAt });
        });
        modelBuilder.Entity<EarningHold>(b =>
        {
            b.ToTable("booking_earning_holds");
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.HasIndex(x => x.BookingId).IsUnique();
            b.HasIndex(x => new { x.Status, x.ReleasesAt });
        });
    }
}

internal sealed class BookingDbContextFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PLASTOIDS_DESIGN_CONNECTION")
            ?? "Server=localhost;Port=3306;Database=plastoids;User=plastoids;Password=plastoids;";
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseMySql(InfrastructureServiceCollectionExtensions.EnsureGuidFormat(connection), ServerVersion.Parse("8.4.0-mysql"))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new BookingDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public Guid TenantId => Tenants.Marketplace;
        public bool IsSystemContext => true;
    }
}
