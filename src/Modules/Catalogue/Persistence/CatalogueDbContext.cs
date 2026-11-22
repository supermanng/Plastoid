using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Catalogue.Persistence;

public sealed class CatalogueDbContext(DbContextOptions<CatalogueDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "catalogue";

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseModule> Modules => Set<CourseModule>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<CourseVersion> Versions => Set<CourseVersion>();
    public DbSet<Offering> Offerings => Set<Offering>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<CourseReviewTask> ReviewTasks => Set<CourseReviewTask>();
    public DbSet<VerifiedTutor> VerifiedTutors => Set<VerifiedTutor>();
    public DbSet<Engagement> Engagements => Set<Engagement>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogueDbContext).Assembly);
    }
}
