using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<LessonProgress> Progress => Set<LessonProgress>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<AssignmentSubmission> Submissions => Set<AssignmentSubmission>();
    public DbSet<Cohort> Cohorts => Set<Cohort>();
    public DbSet<ForumThread> Threads => Set<ForumThread>();
    public DbSet<ForumPost> Posts => Set<ForumPost>();
    public DbSet<OfflinePackage> OfflinePackages => Set<OfflinePackage>();
    public DbSet<PublishedCourseRef> PublishedCourses => Set<PublishedCourseRef>();

    private static PropertyBuilder<List<Guid>> GuidListJson(PropertyBuilder<List<Guid>> builder, string column)
        => builder.HasColumnName(column).HasColumnType("json").HasConversion(
            v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
            v => JsonSerializer.Deserialize<List<Guid>>(v, JsonSerializerOptions.Default) ?? new List<Guid>(),
            new ValueComparer<List<Guid>>((a, c) => (a ?? new List<Guid>()).SequenceEqual(c ?? new List<Guid>()), v => v.Aggregate(0, (h, g) => HashCode.Combine(h, g)), v => v.ToList()));

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
            GuidListJson(b.Property(x => x.ParticipantUserIds), "participant_user_ids_json");
            b.HasIndex(x => x.BookingId).IsUnique();
            b.HasIndex(x => x.RoomId).IsUnique();
            b.HasIndex(x => new { x.HostUserId, x.StartsAt });
            b.HasIndex(x => new { x.Status, x.StartsAt });
            b.HasIndex(x => x.CohortId);
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
        modelBuilder.Entity<Enrolment>(b =>
        {
            b.ToTable("lms_enrolments");
            b.Ignore(x => x.IsActive);
            b.Property(x => x.FundingSource).HasMaxLength(16);
            b.Property(x => x.FundingLabel).HasMaxLength(120);
            b.Property(x => x.SourceKey).HasMaxLength(120);
            b.HasIndex(x => x.SourceKey).IsUnique();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.ProgressPct).HasPrecision(5, 2);
            b.Property(x => x.FinalScore).HasPrecision(5, 2);
            b.HasIndex(x => new { x.LearnerId, x.CourseId }).IsUnique();
            b.HasIndex(x => new { x.CourseId, x.Status });
            b.HasIndex(x => x.CohortId);
        });
        modelBuilder.Entity<LessonProgress>(b =>
        {
            b.ToTable("lms_lesson_progress");
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.PercentComplete).HasPrecision(5, 2);
            b.HasIndex(x => new { x.EnrolmentId, x.LessonId }).IsUnique();
            b.HasOne<Enrolment>().WithMany().HasForeignKey(x => x.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Attempt>(b =>
        {
            b.ToTable("lms_attempts");
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.AnswersJson).HasColumnType("json");
            b.Property(x => x.Score).HasPrecision(6, 2);
            b.Property(x => x.MaxScore).HasPrecision(6, 2);
            b.HasIndex(x => new { x.EnrolmentId, x.LessonId, x.Number }).IsUnique();
            b.HasOne<Enrolment>().WithMany().HasForeignKey(x => x.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AssignmentSubmission>(b =>
        {
            b.ToTable("lms_assignment_submissions");
            b.Ignore(x => x.ScorePercent);
            b.Property(x => x.BlobKey).HasMaxLength(300);
            b.Property(x => x.ContentType).HasMaxLength(100);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.Score).HasPrecision(8, 2);
            b.Property(x => x.MaxScore).HasPrecision(8, 2);
            b.Property(x => x.RubricJson).HasColumnType("json");
            b.Property(x => x.Feedback).HasMaxLength(2000);
            b.HasIndex(x => new { x.EnrolmentId, x.LessonId });
            b.HasOne<Enrolment>().WithMany().HasForeignKey(x => x.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Cohort>(b =>
        {
            b.ToTable("lms_cohorts");
            b.Ignore(x => x.HasSeats);
            b.Ignore(x => x.SeatsLeft);
            b.Property(x => x.Name).HasMaxLength(160);
            b.HasIndex(x => new { x.OfferingId, x.StartsAt });
            b.HasIndex(x => x.TutorUserId);
        });
        modelBuilder.Entity<ForumThread>(b =>
        {
            b.ToTable("lms_forum_threads");
            b.Property(x => x.Title).HasMaxLength(200);
            b.Property(x => x.Body).HasMaxLength(4000);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.HasIndex(x => new { x.CohortId, x.LastActivityAt });
        });
        modelBuilder.Entity<ForumPost>(b =>
        {
            b.ToTable("lms_forum_posts");
            b.Ignore(x => x.IsFlagged);
            b.Property(x => x.Body).HasMaxLength(4000);
            b.Property(x => x.FlagReason).HasMaxLength(48);
            b.HasIndex(x => x.ThreadId);
            b.HasOne<ForumThread>().WithMany().HasForeignKey(x => x.ThreadId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OfflinePackage>(b =>
        {
            b.ToTable("lms_offline_packages");
            b.Property(x => x.ManifestJson).HasColumnType("json");
            b.Property(x => x.KeyCiphertext).HasMaxLength(512);
            b.HasIndex(x => new { x.EnrolmentId, x.IssuedAt });
            b.HasOne<Enrolment>().WithMany().HasForeignKey(x => x.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PublishedCourseRef>(b =>
        {
            b.ToTable("lms_published_courses");
            b.Property(x => x.Slug).HasMaxLength(80);
            GuidListJson(b.Property(x => x.OfferingIds), "offering_ids_json");
            b.HasIndex(x => x.CourseId).IsUnique();
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
