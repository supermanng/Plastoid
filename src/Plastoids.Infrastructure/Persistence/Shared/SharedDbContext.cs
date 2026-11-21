using Microsoft.EntityFrameworkCore;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Infrastructure.Persistence.Shared;

/// <summary>Cross-cutting tables owned by Infrastructure: shared_idempotency_keys.</summary>
public sealed class SharedDbContext(DbContextOptions<SharedDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "shared";

    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdempotencyKey>(b =>
        {
            b.ToTable("shared_idempotency_keys");
            b.HasKey(x => new { x.Key, x.ActorId });
            b.Property(x => x.Key).HasMaxLength(128);
            b.Property(x => x.ActorId).HasColumnType("binary(16)");
            b.Property(x => x.RequestHash).HasMaxLength(64);
            b.Property(x => x.RequestType).HasMaxLength(300);
            b.Property(x => x.ResponseBody).HasColumnType("json");
            b.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
            b.Property(x => x.ExpiresAt).HasColumnType("datetime(6)");
            b.HasIndex(x => x.ExpiresAt);
        });
    }
}

/// <summary>Blueprint build prompt §2.6: replay returns the stored response; a different request hash under the same key → 422.</summary>
public sealed class IdempotencyKey
{
    public string Key { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
