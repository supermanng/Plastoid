using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plastoids.Modules.Identity.Domain;

namespace Plastoids.Modules.Identity.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("identity_tenants");
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Slug).HasMaxLength(64);
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("identity_users");
        b.Property(x => x.PhoneCiphertext).HasMaxLength(512);
        b.Property(x => x.PhoneHash).HasMaxLength(64);
        b.HasIndex(x => x.PhoneHash).IsUnique();
        b.Property(x => x.Email).HasMaxLength(320);
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.PasswordHash).HasMaxLength(512);
        b.Property(x => x.DisplayName).HasMaxLength(120);
        b.Property(x => x.DobCiphertext).HasMaxLength(512);
        b.Property(x => x.MfaSecretCiphertext).HasMaxLength(512);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.CareerGoal).HasMaxLength(200);
        b.Property(x => x.Interests)
            .HasColumnName("interests_json")
            .HasColumnType("json")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>(),
                new ValueComparer<List<string>>((a, c) => (a ?? new List<string>()).SequenceEqual(c ?? new List<string>()), v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode(StringComparison.Ordinal))), v => v.ToList()));
        b.HasIndex(x => x.Status);
    }
}

internal sealed class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> b)
    {
        b.ToTable("identity_organisations");
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.CacNumber).HasMaxLength(32);
        b.HasIndex(x => x.CacNumber).IsUnique();
        b.Property(x => x.SignatoryName).HasMaxLength(120);
        b.Property(x => x.SignatoryEmail).HasMaxLength(320);
        b.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.RejectionReason).HasMaxLength(500);
        b.HasIndex(x => x.VerificationStatus);
    }
}

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> b)
    {
        b.ToTable("identity_memberships");
        b.Property(x => x.Role).HasMaxLength(32);
        b.HasIndex(x => new { x.UserId, x.OrganisationId, x.Role }).IsUnique();
        b.HasIndex(x => x.OrganisationId);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Organisation>().WithMany().HasForeignKey(x => x.OrganisationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("identity_refresh_tokens");
        b.Property(x => x.TokenHash).HasMaxLength(64);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.ReplacedByHash).HasMaxLength(64);
        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.Property(x => x.Amr).HasMaxLength(64);
        b.HasIndex(x => x.UserId);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MfaRecoveryCodeConfiguration : IEntityTypeConfiguration<MfaRecoveryCode>
{
    public void Configure(EntityTypeBuilder<MfaRecoveryCode> b)
    {
        b.ToTable("identity_mfa_recovery_codes");
        b.Property(x => x.CodeHash).HasMaxLength(64);
        b.HasIndex(x => new { x.UserId, x.CodeHash }).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DataExportRequestConfiguration : IEntityTypeConfiguration<DataExportRequest>
{
    public void Configure(EntityTypeBuilder<DataExportRequest> b)
    {
        b.ToTable("identity_data_export_requests");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.JsonBlobKey).HasMaxLength(300);
        b.Property(x => x.PdfBlobKey).HasMaxLength(300);
        b.Property(x => x.FailureReason).HasMaxLength(500);
        b.HasIndex(x => new { x.Status, x.RequestedAt });
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
