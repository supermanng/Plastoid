using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Identity.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ICurrentTenant tenant)
    : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "identity";

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        // Sprint 1 adds User, Organisation, Membership here (blueprint §16).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
