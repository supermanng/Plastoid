using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Identity.Consumers;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Features.Auth.EnableMfa;
using Plastoids.Modules.Identity.Features.Auth.IssueToken;
using Plastoids.Modules.Identity.Features.Auth.RegisterWithEmail;
using Plastoids.Modules.Identity.Features.Auth.RequestOtp;
using Plastoids.Modules.Identity.Features.Auth.VerifyMfa;
using Plastoids.Modules.Identity.Features.Auth.VerifyOtp;
using Plastoids.Modules.Identity.Features.Diagnostics.EmitSmokeEvent;
using Plastoids.Modules.Identity.Features.Me.DeleteMyAccount;
using Plastoids.Modules.Identity.Features.Me.GetDataExport;
using Plastoids.Modules.Identity.Features.Me.GetMe;
using Plastoids.Modules.Identity.Features.Me.RequestDataExport;
using Plastoids.Modules.Identity.Features.Me.UpdateGoals;
using Plastoids.Modules.Identity.Features.Me.UpdateMe;
using Plastoids.Modules.Identity.Features.Organisations.AddMember;
using Plastoids.Modules.Identity.Features.Organisations.ChangeMemberRole;
using Plastoids.Modules.Identity.Features.Organisations.RegisterOrganisation;
using Plastoids.Modules.Identity.Features.Organisations.VerifyOrganisation;
using Plastoids.Modules.Identity.Jobs;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.Modules.Identity.Seed;
using Plastoids.Modules.Identity.Services;
using Quartz;

namespace Plastoids.Modules.Identity;

public sealed class IdentityModule : IModule
{
    public string Name => "identity";
    public Assembly Assembly => typeof(IdentityModule).Assembly;
    public Type DbContextType => typeof(IdentityDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(SmokeTestRequested)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(UserRegistered)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(OrganisationVerified)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<SmokeEventSink>();
        services.AddScoped<OtpService>();
        services.AddScoped<TokenIssuer>();
        services.AddScoped<IPlatformRoles, PlatformRoles>();
        services.AddSingleton<IGoogleIdTokenVerifier, NotConfiguredGoogleIdTokenVerifier>();
        services.AddScoped<IAuthorizationHandler, OrganisationRoleHandler>();
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        services.AddAuthorizationBuilder()
            .AddPolicy(IdentityPolicies.OrganisationOwner, p => p.RequireAuthenticatedUser().AddRequirements(new OrganisationRoleRequirement([OrganisationRoles.Owner])))
            .AddPolicy(IdentityPolicies.OrganisationMember, p => p.RequireAuthenticatedUser().AddRequirements(new OrganisationRoleRequirement(OrganisationRoles.All)))
            .AddPolicy(IdentityPolicies.OrganisationFinance, p => p.RequireAuthenticatedUser().AddRequirements(new OrganisationRoleRequirement([OrganisationRoles.Owner, OrganisationRoles.Finance])));

        services.AddQuartz(q =>
        {
            var key = new JobKey(DataExportJob.JobName);
            q.AddJob<DataExportJob>(j => j.WithIdentity(key).StoreDurably());
            q.AddTrigger(t => t.ForJob(key).WithIdentity(DataExportJob.JobName + "-trigger")
                .StartNow().WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromSeconds(15)).RepeatForever()));
        });

        if (configuration.GetValue<bool>($"{SeedOptions.SectionName}:Enabled"))
        {
            services.AddHostedService<DevelopmentSeeder>();
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var identity = endpoints.MapGroup("/identity").WithTags("Identity");

        var auth = identity.MapGroup("/auth");
        RequestOtpEndpoint.Map(auth);
        VerifyOtpEndpoint.Map(auth);
        RegisterWithEmailEndpoint.Map(auth);
        IssueTokenEndpoint.Map(auth);
        VerifyMfaEndpoint.Map(auth);

        var me = identity.MapGroup("/me");
        GetMeEndpoint.Map(me);
        UpdateMeEndpoint.Map(me);
        UpdateGoalsEndpoint.Map(me);
        EnableMfaEndpoint.Map(me);
        RequestDataExportEndpoint.Map(me);
        GetDataExportEndpoint.Map(me);
        DeleteMyAccountEndpoint.Map(me);

        var organisations = identity.MapGroup("/organisations");
        RegisterOrganisationEndpoint.Map(organisations);
        AddMemberEndpoint.Map(organisations);
        ChangeMemberRoleEndpoint.Map(organisations);

        // Staff endpoints live under /api/v1/admin/* (IP allow-list middleware + staff-admin policy).
        var admin = endpoints.MapGroup("/admin/identity").WithTags("Identity (staff)");
        VerifyOrganisationEndpoint.Map(admin);

        if (endpoints.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<bool>("Diagnostics:Enabled"))
        {
            EmitSmokeEventEndpoint.Map(identity);
        }
    }

    public IEnumerable<ConsumerRegistration> Consumers() =>
    [
        ConsumerRegistry.For<SmokeTestRequested, SmokeTestRequestedConsumer, IdentityDbContext>(Name),
    ];
}
