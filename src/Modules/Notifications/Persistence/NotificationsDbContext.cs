using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plastoids.Infrastructure;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Notifications.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Notifications.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "notifications";

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<NotificationTemplate> Templates => Set<NotificationTemplate>();
    public DbSet<ChannelPreference> Preferences => Set<ChannelPreference>();
    public DbSet<MessageThread> Threads => Set<MessageThread>();
    public DbSet<ThreadMessage> Messages => Set<ThreadMessage>();
    public DbSet<Announcement> Announcements => Set<Announcement>();

    private static PropertyBuilder<List<string>> StringListJson(PropertyBuilder<List<string>> builder, string column)
        => builder.HasColumnName(column).HasColumnType("json").HasConversion(
            v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
            v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>(),
            new ValueComparer<List<string>>((a, c) => (a ?? new List<string>()).SequenceEqual(c ?? new List<string>()), v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s)), v => v.ToList()));

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(b =>
        {
            b.ToTable("notifications_notifications");
            b.Property(x => x.TemplateKey).HasMaxLength(80);
            b.Property(x => x.DedupKey).HasMaxLength(200);
            b.Property(x => x.Subject).HasMaxLength(200);
            b.Property(x => x.Body).HasMaxLength(4000);
            b.Property(x => x.DataJson).HasColumnType("json");
            b.HasIndex(x => new { x.RecipientId, x.CreatedAt });
            b.HasIndex(x => x.DedupKey).IsUnique();
        });

        modelBuilder.Entity<Delivery>(b =>
        {
            b.ToTable("notifications_deliveries");
            b.Property(x => x.Channel).HasMaxLength(16);
            b.Property(x => x.Destination).HasMaxLength(200);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.ProviderReference).HasMaxLength(120);
            b.Property(x => x.Error).HasMaxLength(500);
            b.HasIndex(x => x.NotificationId);
            b.HasIndex(x => new { x.Status, x.DeferredUntil });
        });

        modelBuilder.Entity<NotificationTemplate>(b =>
        {
            b.ToTable("notifications_templates");
            b.Property(x => x.Key).HasMaxLength(80);
            b.Property(x => x.Channel).HasMaxLength(16);
            b.Property(x => x.Subject).HasMaxLength(200);
            b.Property(x => x.Body).HasMaxLength(4000);
            b.HasIndex(x => new { x.Key, x.Channel, x.Version }).IsUnique();
        });

        modelBuilder.Entity<ChannelPreference>(b =>
        {
            b.ToTable("notifications_preferences");
            StringListJson(b.Property(x => x.ChannelOrder), "channel_order_json");
            StringListJson(b.Property(x => x.OptedOut), "opted_out_json");
            b.Property(x => x.TimeZone).HasMaxLength(64);
            b.HasIndex(x => x.UserId).IsUnique();
        });

        modelBuilder.Entity<MessageThread>(b =>
        {
            b.ToTable("notifications_threads");
            b.Property(x => x.Subject).HasMaxLength(200);
            b.HasIndex(x => new { x.LearnerUserId, x.LastMessageAt });
            b.HasIndex(x => new { x.TutorUserId, x.LastMessageAt });
            b.HasIndex(x => x.IsFlagged);
        });

        modelBuilder.Entity<ThreadMessage>(b =>
        {
            b.ToTable("notifications_thread_messages");
            b.Ignore(x => x.IsFlagged);
            b.Property(x => x.Body).HasMaxLength(4000);
            b.Property(x => x.FlagReason).HasMaxLength(40);
            b.HasIndex(x => new { x.ThreadId, x.SentAt });
        });

        modelBuilder.Entity<Announcement>(b =>
        {
            b.ToTable("notifications_announcements");
            b.Property(x => x.Title).HasMaxLength(200);
            b.Property(x => x.Body).HasMaxLength(4000);
            b.HasIndex(x => new { x.CohortId, x.PostedAt });
        });
    }
}

internal sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PLASTOIDS_DESIGN_CONNECTION")
            ?? "Server=localhost;Port=3306;Database=plastoids;User=plastoids;Password=plastoids;";
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseMySql(InfrastructureServiceCollectionExtensions.EnsureGuidFormat(connection), ServerVersion.Parse("8.4.0-mysql"))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new NotificationsDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public Guid TenantId => Tenants.Marketplace;
        public bool IsSystemContext => true;
    }
}
