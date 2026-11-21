using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plastoids.Modules.Identity.Domain;

namespace Plastoids.Modules.Identity.Persistence.Configurations;

internal sealed class RoleGrantProposalConfiguration : IEntityTypeConfiguration<RoleGrantProposal>
{
    public void Configure(EntityTypeBuilder<RoleGrantProposal> b)
    {
        b.ToTable("identity_role_grants");
        b.Property(x => x.Role).HasMaxLength(16);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.DecisionNote).HasMaxLength(1000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(x => new { x.UserId, x.Role, x.Status });
        b.HasIndex(x => x.Status);
    }
}
