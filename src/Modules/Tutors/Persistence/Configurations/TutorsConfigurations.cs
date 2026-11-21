using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plastoids.Modules.Tutors.Domain;

namespace Plastoids.Modules.Tutors.Persistence.Configurations;

internal static class JsonList
{
    public static PropertyBuilder<List<string>> AsJson(this PropertyBuilder<List<string>> builder, string column)
        => builder.HasColumnName(column).HasColumnType("json").HasConversion(
            v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
            v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>(),
            new ValueComparer<List<string>>((a, c) => (a ?? new List<string>()).SequenceEqual(c ?? new List<string>()), v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode(StringComparison.Ordinal))), v => v.ToList()));
}

internal sealed class TutorProfileConfiguration : IEntityTypeConfiguration<TutorProfile>
{
    public void Configure(EntityTypeBuilder<TutorProfile> b)
    {
        b.ToTable("tutors_profiles");
        b.HasIndex(x => x.UserId).IsUnique();
        b.Property(x => x.Headline).HasMaxLength(160);
        b.Property(x => x.Bio).HasColumnType("text");
        b.Property(x => x.Subjects).AsJson("subjects_json");
        b.Property(x => x.Languages).AsJson("languages_json");
        b.Property(x => x.Formats).AsJson("formats_json");
        b.Property(x => x.TimeZone).HasMaxLength(64);
        b.Property(x => x.Level).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.QualityScore).HasPrecision(6, 3);
        b.Property(x => x.RatingAverage).HasPrecision(4, 2);
        b.Property(x => x.VerifiedFullName).HasMaxLength(160);
        b.Property(x => x.DemoLessonBlobKey).HasMaxLength(300);
        b.Property(x => x.CalendarAccount).HasMaxLength(320);
        b.HasIndex(x => new { x.Status, x.Level });
        // FULLTEXT on headline + bio for discovery (build prompt Sprint 2).
        b.HasIndex(x => new { x.Headline, x.Bio }).IsFullText().HasDatabaseName("ft_tutors_profiles_headline_bio");
    }
}

internal sealed class CredentialConfiguration : IEntityTypeConfiguration<Credential>
{
    public void Configure(EntityTypeBuilder<Credential> b)
    {
        b.ToTable("tutors_credentials");
        b.Property(x => x.Type).HasMaxLength(32);
        b.Property(x => x.Issuer).HasMaxLength(200);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.EvidenceBlobKey).HasMaxLength(300);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ReviewNote).HasMaxLength(500);
        b.HasIndex(x => new { x.TutorProfileId, x.Status });
        b.HasOne<TutorProfile>().WithMany().HasForeignKey(x => x.TutorProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class VerificationCheckConfiguration : IEntityTypeConfiguration<VerificationCheck>
{
    public void Configure(EntityTypeBuilder<VerificationCheck> b)
    {
        b.ToTable("tutors_verification_checks");
        b.Property(x => x.Type).HasMaxLength(32);
        b.Property(x => x.Provider).HasMaxLength(64);
        b.Property(x => x.Reference).HasMaxLength(120);
        b.Property(x => x.Result).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Detail).HasMaxLength(500);
        b.HasIndex(x => new { x.TutorProfileId, x.Type });
        b.HasOne<TutorProfile>().WithMany().HasForeignKey(x => x.TutorProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AvailabilityConfiguration : IEntityTypeConfiguration<Availability>
{
    public void Configure(EntityTypeBuilder<Availability> b)
    {
        b.ToTable("tutors_availability");
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Day).HasConversion<string?>().HasMaxLength(16);
        b.Property(x => x.Start).HasColumnType("time(0)");
        b.Property(x => x.End).HasColumnType("time(0)");
        b.HasIndex(x => x.TutorProfileId);
        b.HasOne<TutorProfile>().WithMany().HasForeignKey(x => x.TutorProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PriceRuleConfiguration : IEntityTypeConfiguration<PriceRule>
{
    public void Configure(EntityTypeBuilder<PriceRule> b)
    {
        b.ToTable("tutors_price_rules");
        b.Property(x => x.Format).HasMaxLength(32);
        b.HasIndex(x => new { x.TutorProfileId, x.Format, x.DurationMinutes }).IsUnique();
        b.HasOne<TutorProfile>().WithMany().HasForeignKey(x => x.TutorProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PayoutAccountConfiguration : IEntityTypeConfiguration<PayoutAccount>
{
    public void Configure(EntityTypeBuilder<PayoutAccount> b)
    {
        b.ToTable("tutors_payout_accounts");
        b.HasIndex(x => x.TutorProfileId).IsUnique();
        b.Property(x => x.BankCode).HasMaxLength(16);
        b.Property(x => x.AccountNumberCiphertext).HasMaxLength(512);
        b.Property(x => x.AccountNumberMasked).HasMaxLength(16);
        b.Property(x => x.AccountName).HasMaxLength(160);
        b.HasOne<TutorProfile>().WithMany().HasForeignKey(x => x.TutorProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ReviewQueueItemConfiguration : IEntityTypeConfiguration<ReviewQueueItem>
{
    public void Configure(EntityTypeBuilder<ReviewQueueItem> b)
    {
        b.ToTable("tutors_review_queue");
        b.Property(x => x.Kind).HasMaxLength(32);
        b.HasIndex(x => new { x.CompletedAt, x.DueAt });
        b.HasIndex(x => new { x.Kind, x.ReferenceId });
        b.HasOne<TutorProfile>().WithMany().HasForeignKey(x => x.TutorProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}
