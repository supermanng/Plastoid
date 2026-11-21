using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using Plastoids.Infrastructure;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Lms.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Lms.Persistence;

public sealed class LmsDbContext(DbContextOptions<LmsDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "lms";

    public DbSet<LiveSession> LiveSessions => Set<LiveSession>();
    public DbSet<AttendanceEvent> Attendance => Set<AttendanceEvent>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LiveSession>(b =>
        {
            b.ToTable("lms_live_sessions");
            b.Property(x => x.Source).HasMaxLength(16);
            b.Property(x => x.Title).HasMaxLength(200);
            b.Property(x => x.Provider).HasMaxLength(32);
            b.Property(x => x.RoomId).HasMaxLength(120);
            b.Property(x => x.RoomUrl).HasMaxLength(500);
            b.Property(x => x.RecordingUrl).HasMaxLength(500);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.ParticipantUserIds).HasColumnName("participant_user_ids_json").HasColumnType("json").HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<Guid>>(v, JsonSerializerOptions.Default) ?? new List<Guid>(),
                new ValueComparer<List<Guid>>((a, c) => (a ?? new List<Guid>()).SequenceEqual(c ?? new List<Guid>()), v => v.Aggregate(0, (h, g) => HashCode.Combine(h, g)), v => v.ToList()));
            b.HasIndex(x => x.BookingId).IsUnique();
            b.HasIndex(x => x.RoomId).IsUnique();
            b.HasIndex(x => new { x.HostUserId, x.StartsAt });
            b.HasIndex(x => new { x.Status, x.StartsAt });
        });
        modelBuilder.Entity<AttendanceEvent>(b =>
        {
            b.ToTable("lms_attendance_events");
            b.Ignore(x => x.IsOpen);
            b.Property(x => x.ProviderEventId).HasMaxLength(120);
            b.HasIndex(x => new { x.LiveSessionId, x.UserId });
            b.HasIndex(x => x.ProviderEventId).IsUnique();
            b.HasOne<LiveSession>().WithMany().HasForeignKey(x => x.LiveSessionId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class LmsDbContextFactory : IDesignTimeDbContextFactory<LmsDbContext>
{
    public LmsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PLASTOIDS_DESIGN_CONNECTION")
            ?? "Server=localhost;Port=3306;Database=plastoids;User=plastoids;Password=plastoids;";
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseMySql(InfrastructureServiceCollectionExtensions.EnsureGuidFormat(connection), ServerVersion.Parse("8.4.0-mysql"))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new LmsDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public Guid TenantId => Tenants.Marketplace;
        public bool IsSystemContext => true;
    }
}
