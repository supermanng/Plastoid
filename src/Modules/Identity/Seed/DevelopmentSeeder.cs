using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plastoids.Infrastructure.Security;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Identity.Seed;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";
    public bool Enabled { get; set; }
    public string StaffPhone { get; set; } = "+2348000000001";
    public string StaffEmail { get; set; } = "staff@plastoids.local";
    public string StaffPassword { get; set; } = string.Empty;

    /// <summary>Base32 TOTP secret for the seeded staff user so local tools and tests can complete MFA.</summary>
    public string StaffMfaSecret { get; set; } = string.Empty;
}

/// <summary>
/// Sprint 1 seed: tenant zero, one staff user (MFA enrolled) and three learners. Idempotent; runs after migrations
/// when Seed:Enabled is true (Development and tests only — never in production configuration).
/// </summary>
internal sealed class DevelopmentSeeder(IServiceProvider provider, IConfiguration configuration, ILogger<DevelopmentSeeder> logger) : IHostedService
{
    public static readonly (string Phone, string Name, string Dob)[] Learners =
    [
        ("+2348000000011", "Chiamaka Okafor", "2003-04-12"),
        ("+2348000000012", "Tunde Bakare", "1997-09-30"),
        ("+2348000000013", "Amina Yusuf", "2001-01-05"),
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = configuration.GetSection(SeedOptions.SectionName).Get<SeedOptions>() ?? new SeedOptions();
        if (!options.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.StaffPassword) || string.IsNullOrWhiteSpace(options.StaffMfaSecret))
        {
            throw new InvalidOperationException("Seed:StaffPassword and Seed:StaffMfaSecret are required when Seed:Enabled is true.");
        }

        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var encryptor = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        if (!await db.Tenants.AnyAsync(t => t.Id == Tenants.Marketplace, cancellationToken))
        {
            db.Tenants.Add(Tenant.Create(Tenants.Marketplace, "Plastoids marketplace", "plastoids"));
        }

        var staffHash = encryptor.BlindIndex(options.StaffPhone);
        if (!await db.Users.AnyAsync(u => u.PhoneHash == staffHash, cancellationToken))
        {
            var staff = User.Register(Tenants.Marketplace, encryptor.Encrypt(options.StaffPhone), staffHash, encryptor.Encrypt("1990-06-15"), new DateOnly(1990, 6, 15), "Seyi Adeyemi (staff)", clock.UtcNow).Value;
            staff.SetEmailAndPassword(options.StaffEmail, hasher.Hash(options.StaffPassword));
            staff.BeginMfaEnrolment(encryptor.Encrypt(options.StaffMfaSecret));
            staff.ConfirmMfa();
            db.Users.Add(staff);
            db.Memberships.Add(Membership.Platform(Tenants.Marketplace, staff.Id, RoleNames.Staff));
            logger.LogInformation("Seeded staff user {Email}", options.StaffEmail);
        }

        foreach (var (phone, name, dob) in Learners)
        {
            var hash = encryptor.BlindIndex(phone);
            if (await db.Users.AnyAsync(u => u.PhoneHash == hash, cancellationToken))
            {
                continue;
            }

            var date = DateOnly.Parse(dob, System.Globalization.CultureInfo.InvariantCulture);
            var learner = User.Register(Tenants.Marketplace, encryptor.Encrypt(phone), hash, encryptor.Encrypt(dob), date, name, clock.UtcNow).Value;
            db.Users.Add(learner);
            db.Memberships.Add(Membership.Platform(Tenants.Marketplace, learner.Id, RoleNames.Learner));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
