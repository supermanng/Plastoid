using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.Booking.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payouts.Consumers;
using Plastoids.Modules.Payouts.Contracts;
using Plastoids.Modules.Payouts.Features.Batches.ApprovePayoutBatch;
using Plastoids.Modules.Payouts.Features.Batches.CreatePayoutBatch;
using Plastoids.Modules.Payouts.Features.Batches.HandlePayoutStatusWebhook;
using Plastoids.Modules.Payouts.Features.Batches.ListPayoutQueue;
using Plastoids.Modules.Payouts.Features.Batches.SubmitPayoutBatch;
using Plastoids.Modules.Payouts.Features.Earnings.GetMyEarnings;
using Plastoids.Modules.Payouts.Features.Holds.PlaceHold;
using Plastoids.Modules.Payouts.Features.Organisations.GetOrganisationEarnings;
using Plastoids.Modules.Payouts.Features.Organisations.SetOrganisationPayoutAccount;
using Plastoids.Modules.Payouts.Jobs;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.Modules.Payouts.Services;
using Plastoids.Modules.Sponsorship.Contracts;
using Quartz;

namespace Plastoids.Modules.Payouts;

public sealed class PayoutsModule : IModule
{
    public string Name => "payouts";
    public Assembly Assembly => typeof(PayoutsModule).Assembly;
    public Type DbContextType => typeof(PayoutsDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(BatchApproved)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(PayoutSettled)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(PayoutFailed)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(NotificationRequested)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PayoutOptions>(configuration.GetSection(PayoutOptions.SectionName));
        services.AddScoped<PayableCalculator>();
        services.AddScoped<PayeeDestinations>();
        services.AddScoped<EarningsStatements>();
        services.AddScoped<PayoutRunner>();
        services.AddScoped<PayoutSettlement>();
        services.AddScoped<IDisputeQueue, DisputeQueue>();
        services.AddScoped<EarningsReleaseJob>();
        services.AddScoped<WeeklyPayoutRunJob>();
        services.AddQuartz(q =>
        {
            var release = new JobKey(EarningsReleaseJob.JobName);
            q.AddJob<EarningsReleaseJob>(j => j.WithIdentity(release).StoreDurably());
            q.AddTrigger(t => t.ForJob(release).WithIdentity(release.Name + "-trigger").StartNow().WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(15)).RepeatForever()));
            var weekly = new JobKey(WeeklyPayoutRunJob.JobName);
            q.AddJob<WeeklyPayoutRunJob>(j => j.WithIdentity(weekly).StoreDurably());
            q.AddTrigger(t => t.ForJob(weekly).WithIdentity(weekly.Name + "-trigger").WithCronSchedule("0 0 8 ? * THU")); // Thursdays 09:00 Lagos
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var payouts = endpoints.MapGroup("/payouts").WithTags("Payouts");
        GetMyEarningsEndpoints.Map(payouts);
        HandlePayoutStatusWebhookEndpoint.Map(payouts);

        var organisations = payouts.MapGroup("/organisations/{organisationId:guid}");
        SetOrganisationPayoutAccountEndpoint.Map(organisations);
        GetOrganisationEarningsEndpoint.Map(organisations);

        var admin = endpoints.MapGroup("/admin/payouts").WithTags("Payouts (staff)");
        ListPayoutQueueEndpoint.Map(admin);
        CreatePayoutBatchEndpoint.Map(admin);
        ApprovePayoutBatchEndpoints.Map(admin);
        SubmitPayoutBatchEndpoint.Map(admin);
        HoldEndpoints.Map(admin);
    }

    public IEnumerable<ConsumerRegistration> Consumers() =>
    [
        ConsumerRegistry.For<PaymentSucceeded, PaymentSucceededConsumer, PayoutsDbContext>(Name),
        ConsumerRegistry.For<EarningReleased, EarningReleasedConsumer, PayoutsDbContext>(Name),
        ConsumerRegistry.For<RefundIssued, RefundIssuedConsumer, PayoutsDbContext>(Name),
        ConsumerRegistry.For<TutorEarningsAccrued, TutorEarningsAccruedConsumer, PayoutsDbContext>(Name),
    ];
}
