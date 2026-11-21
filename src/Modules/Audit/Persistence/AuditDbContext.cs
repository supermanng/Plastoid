using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Audit.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Audit.Persistence;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "audit";

    public DbSet<AuditEntry> Events => Set<AuditEntry>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEntry>(b =>
        {
            b.ToTable("audit_events");
            b.Property(x => x.Action).HasMaxLength(200);
            b.Property(x => x.EntityName).HasMaxLength(120);
            b.Property(x => x.Before).HasColumnType("json");
            b.Property(x => x.After).HasColumnType("json");
            b.Property(x => x.Ip).HasMaxLength(64);
            b.Property(x => x.CorrelationId).HasMaxLength(64);
            b.HasIndex(x => x.EventId).IsUnique();
            b.HasIndex(x => new { x.EntityName, x.EntityId });
            b.HasIndex(x => x.ActorId);
            b.HasIndex(x => x.OccurredAt);
        });
    }
}
