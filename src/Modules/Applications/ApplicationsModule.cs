using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.Applications.Consumers;
using Plastoids.Modules.Applications.Contracts;
using Plastoids.Modules.Applications.Features.Applying.AcceptOffer;
using Plastoids.Modules.Applications.Features.Applying.EntryAssessment;
using Plastoids.Modules.Applications.Features.Applying.FileAppeal;
using Plastoids.Modules.Applications.Features.Applying.SubmitApplication;
using Plastoids.Modules.Applications.Features.Review.ApproveShortlist;
using Plastoids.Modules.Applications.Features.Review.BulkDecide;
using Plastoids.Modules.Applications.Features.Review.ListReviewQueue;
using Plastoids.Modules.Applications.Features.Review.ResolveAppeal;
using Plastoids.Modules.Applications.Features.Review.ScoreApplication;
using Plastoids.Modules.Applications.Features.Review.SendOffers;
using Plastoids.Modules.Applications.Jobs;
using Plastoids.Modules.Applications.Persistence;
using Plastoids.Modules.Applications.Services;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;
using Quartz;

namespace Plastoids.Modules.Applications;

public sealed class ApplicationsModule : IModule
{
    public string Name => "applications";
    public Assembly Assembly => typeof(ApplicationsModule).Assembly;
    public Type DbContextType => typeof(ApplicationsDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(ApplicationSubmitted)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(DecisionRecorded)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(OfferSent)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(OfferAccepted)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(NotificationRequested)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<OfferService>();
        services.AddScoped<ApplicationAccess>();
        services.AddScoped<WaitlistBackfillJob>();
        services.AddQuartz(q =>
        {
            var backfill = new JobKey(WaitlistBackfillJob.JobName);
            q.AddJob<WaitlistBackfillJob>(j => j.WithIdentity(backfill).StoreDurably());
            q.AddTrigger(t => t.ForJob(backfill).WithIdentity(backfill.Name + "-trigger").StartNow().WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromHours(1)).RepeatForever()));
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var applications = endpoints.MapGroup("/applications").WithTags("Applications");
        SubmitApplicationEndpoint.Map(applications);
        EntryAssessmentEndpoints.Map(applications);
        AcceptOfferEndpoints.Map(applications);
        FileAppealEndpoint.Map(applications);
        ApproveShortlistEndpoint.Map(applications);

        var admin = endpoints.MapGroup("/admin/applications").WithTags("Applications (staff)");
        ListReviewQueueEndpoint.Map(admin);
        ScoreApplicationEndpoint.Map(admin);
        BulkDecideEndpoint.Map(admin);
        SendOffersEndpoint.Map(admin);
        ResolveAppealEndpoint.Map(admin);
    }

    public IEnumerable<ConsumerRegistration> Consumers() =>
    [
        ConsumerRegistry.For<SeatReleased, SeatReleasedConsumer, ApplicationsDbContext>(Name),
        ConsumerRegistry.For<PaymentSucceeded, CoPayPaymentConsumer, ApplicationsDbContext>(Name),
        ConsumerRegistry.For<ProgrammeFunded, ProgrammeFundedConsumer, ApplicationsDbContext>(Name),
    ];
}
