using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.Tutors.Contracts;
using Plastoids.Modules.Tutors.Features.Discovery.GetTutorAvailability;
using Plastoids.Modules.Tutors.Features.Discovery.ListTutorsForDiscovery;
using Plastoids.Modules.Tutors.Features.Onboarding.ApplyAsTutor;
using Plastoids.Modules.Tutors.Features.Onboarding.ReviewCredential;
using Plastoids.Modules.Tutors.Features.Onboarding.ReviewSubject;
using Plastoids.Modules.Tutors.Features.Onboarding.SubmitDemoLesson;
using Plastoids.Modules.Tutors.Features.Onboarding.SubmitIdentityCheck;
using Plastoids.Modules.Tutors.Features.Onboarding.UploadCredential;
using Plastoids.Modules.Tutors.Features.Profile.ConnectGoogleCalendar;
using Plastoids.Modules.Tutors.Features.Profile.GetTutorDashboard;
using Plastoids.Modules.Tutors.Features.Profile.SetAvailability;
using Plastoids.Modules.Tutors.Features.Profile.SetPayoutAccount;
using Plastoids.Modules.Tutors.Features.Profile.SetPricing;
using Plastoids.Modules.Tutors.Jobs;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.Modules.Tutors.Services;
using Quartz;

namespace Plastoids.Modules.Tutors;

public sealed class TutorsModule : IModule
{
    public string Name => "tutors";
    public Assembly Assembly => typeof(TutorsModule).Assembly;
    public Type DbContextType => typeof(TutorsDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(TutorVerified)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(ReviewSlaBreached)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TutorAccess>();
        services.AddScoped<ITutorSchedule, TutorSchedule>();
        services.AddScoped<ITutorPayoutAccounts, TutorPayoutAccounts>();
        services.AddScoped<ITutorVerificationQueue, TutorVerificationQueue>();
        services.AddQuartz(q =>
        {
            var key = new JobKey(ReviewSlaJob.JobName);
            q.AddJob<ReviewSlaJob>(j => j.WithIdentity(key).StoreDurably());
            q.AddTrigger(t => t.ForJob(key).WithIdentity(ReviewSlaJob.JobName + "-trigger")
                .StartNow().WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(10)).RepeatForever()));
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var tutors = endpoints.MapGroup("/tutors").WithTags("Tutors");
        ListTutorsForDiscoveryEndpoint.Map(tutors);
        GetTutorAvailabilityEndpoint.Map(tutors);
        ApplyAsTutorEndpoint.Map(tutors);

        var me = tutors.MapGroup("/me");
        GetTutorDashboardEndpoint.Map(me);
        SubmitIdentityCheckEndpoint.Map(me);
        UploadCredentialEndpoint.Map(me);
        SubmitDemoLessonEndpoint.Map(me);
        SetAvailabilityEndpoint.Map(me);
        SetPricingEndpoint.Map(me);
        ConnectGoogleCalendarEndpoint.Map(me);
        SetPayoutAccountEndpoint.Map(me);

        var admin = endpoints.MapGroup("/admin/tutors").WithTags("Tutors (staff)");
        ReviewCredentialEndpoint.Map(admin);
        ReviewSubjectEndpoint.Map(admin);
    }

    public IEnumerable<ConsumerRegistration> Consumers() => [];
}
