using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Plastoids.Infrastructure;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Certificates.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Certificates.Persistence;

public sealed class CertificatesDbContext(DbContextOptions<CertificatesDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "certificates";

    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<VerificationLog> Verifications => Set<VerificationLog>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Certificate>(b =>
        {
            b.ToTable("certificates_certificates");
            b.Ignore(x => x.IsValid);
            b.Ignore(x => x.HashMatches);
            b.HasIndex(x => x.EnrolmentId).IsUnique();
            b.Property(x => x.LearnerName).HasMaxLength(160);
            b.Property(x => x.CourseTitle).HasMaxLength(200);
            b.Property(x => x.FinalScore).HasPrecision(5, 2);
            b.Property(x => x.PublicCode).HasMaxLength(16);
            b.HasIndex(x => x.PublicCode).IsUnique();
            b.Property(x => x.Hash).HasMaxLength(64);
            b.Property(x => x.TemplateKey).HasMaxLength(80);
            b.Property(x => x.RevocationReason).HasMaxLength(500);
            b.HasIndex(x => x.LearnerId);
        });
        modelBuilder.Entity<VerificationLog>(b =>
        {
            b.ToTable("certificates_verification_logs");
            b.Property(x => x.PublicCode).HasMaxLength(16);
            b.Property(x => x.Outcome).HasMaxLength(16);
            b.Property(x => x.Ip).HasMaxLength(64);
            b.HasIndex(x => new { x.PublicCode, x.VerifiedAt });
        });
    }
}

internal sealed class CertificatesDbContextFactory : IDesignTimeDbContextFactory<CertificatesDbContext>
{
    public CertificatesDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PLASTOIDS_DESIGN_CONNECTION")
            ?? "Server=localhost;Port=3306;Database=plastoids;User=plastoids;Password=plastoids;";
        var options = new DbContextOptionsBuilder<CertificatesDbContext>()
            .UseMySql(InfrastructureServiceCollectionExtensions.EnsureGuidFormat(connection), ServerVersion.Parse("8.4.0-mysql"))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new CertificatesDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public Guid TenantId => Tenants.Marketplace;
        public bool IsSystemContext => true;
    }
}
