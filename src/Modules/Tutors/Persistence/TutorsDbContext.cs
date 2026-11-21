using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Tutors.Persistence;

public sealed class TutorsDbContext(DbContextOptions<TutorsDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "tutors";

    public DbSet<TutorProfile> Profiles => Set<TutorProfile>();
    public DbSet<Credential> Credentials => Set<Credential>();
    public DbSet<VerificationCheck> Checks => Set<VerificationCheck>();
    public DbSet<Availability> Availabilities => Set<Availability>();
    public DbSet<PriceRule> PriceRules => Set<PriceRule>();
    public DbSet<PayoutAccount> PayoutAccounts => Set<PayoutAccount>();
    public DbSet<ReviewQueueItem> ReviewQueue => Set<ReviewQueueItem>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TutorsDbContext).Assembly);
    }
}
