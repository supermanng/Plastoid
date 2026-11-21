using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plastoids.Modules.Catalogue.Domain;

namespace Plastoids.Modules.Catalogue.Persistence.Configurations;

internal static class JsonList
{
    public static PropertyBuilder<List<string>> AsJson(this PropertyBuilder<List<string>> builder, string column)
        => builder.HasColumnName(column).HasColumnType("json").HasConversion(
            v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
            v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>(),
            new ValueComparer<List<string>>((a, c) => (a ?? new List<string>()).SequenceEqual(c ?? new List<string>()), v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode(StringComparison.Ordinal))), v => v.ToList()));
}

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> b)
    {
        b.ToTable("catalogue_courses");
        b.Property(x => x.OwnerType).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Slug).HasMaxLength(80);
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Summary).HasColumnType("text");
        b.Property(x => x.Level).HasMaxLength(16);
        b.Property(x => x.Language).HasMaxLength(8);
        b.Property(x => x.Category).HasMaxLength(64);
        b.Property(x => x.Skills).AsJson("skills_json");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ReviewNote).HasMaxLength(500);
        b.Property(x => x.RatingAverage).HasPrecision(4, 2);
        b.HasIndex(x => new { x.Status, x.Category });
        b.HasIndex(x => x.OwnerUserId);
        b.HasIndex(x => new { x.Title, x.Summary }).IsFullText().HasDatabaseName("ft_catalogue_courses_title_summary");
    }
}

internal sealed class CourseModuleConfiguration : IEntityTypeConfiguration<CourseModule>
{
    public void Configure(EntityTypeBuilder<CourseModule> b)
    {
        b.ToTable("catalogue_modules");
        b.Property(x => x.Title).HasMaxLength(200);
        b.HasIndex(x => new { x.CourseId, x.Order });
        b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> b)
    {
        b.ToTable("catalogue_lessons");
        b.Property(x => x.Type).HasMaxLength(16);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.ContentJson).HasColumnType("json");
        b.HasIndex(x => new { x.ModuleId, x.Order });
        b.HasIndex(x => x.CourseId);
        b.HasOne<CourseModule>().WithMany().HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> b)
    {
        b.ToTable("catalogue_media_assets");
        b.Property(x => x.Provider).HasMaxLength(32);
        b.Property(x => x.UploadId).HasMaxLength(120);
        b.HasIndex(x => new { x.Provider, x.UploadId }).IsUnique();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.PlaybackId).HasMaxLength(120);
        b.Property(x => x.Renditions).HasMaxLength(200);
        b.Property(x => x.FailureReason).HasMaxLength(500);
        b.HasIndex(x => x.LessonId);
        b.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CourseVersionConfiguration : IEntityTypeConfiguration<CourseVersion>
{
    public void Configure(EntityTypeBuilder<CourseVersion> b)
    {
        b.ToTable("catalogue_course_versions");
        b.Property(x => x.OutlineJson).HasColumnType("json");
        b.HasIndex(x => new { x.CourseId, x.VersionNo }).IsUnique();
        b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OfferingConfiguration : IEntityTypeConfiguration<Offering>
{
    public void Configure(EntityTypeBuilder<Offering> b)
    {
        b.ToTable("catalogue_offerings");
        b.Property(x => x.Format).HasMaxLength(16);
        b.Property(x => x.FundingTypes).AsJson("funding_types_json");
        b.HasIndex(x => new { x.CourseId, x.Active });
        b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.ToTable("catalogue_reviews");
        b.Property(x => x.Body).HasMaxLength(2000);
        b.Property(x => x.ProgressAtReview).HasPrecision(5, 2);
        b.HasIndex(x => new { x.CourseId, x.LearnerId }).IsUnique();
        b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CourseReviewTaskConfiguration : IEntityTypeConfiguration<CourseReviewTask>
{
    public void Configure(EntityTypeBuilder<CourseReviewTask> b)
    {
        b.ToTable("catalogue_review_tasks");
        b.Property(x => x.AutomatedChecksJson).HasColumnType("json");
        b.Property(x => x.Outcome).HasMaxLength(16);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.CourseId, x.Outcome });
        b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class VerifiedTutorConfiguration : IEntityTypeConfiguration<VerifiedTutor>
{
    public void Configure(EntityTypeBuilder<VerifiedTutor> b)
    {
        b.ToTable("catalogue_verified_tutors");
        b.HasIndex(x => x.TutorId).IsUnique();
        b.HasIndex(x => x.UserId);
        b.Property(x => x.Level).HasMaxLength(16);
    }
}
