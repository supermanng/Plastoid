using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.Applications.Contracts;
using Plastoids.Modules.Certificates.Contracts;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Sponsorship.Consumers;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.Modules.Sponsorship.Features.Campaigns.ApproveQuote;
using Plastoids.Modules.Sponsorship.Features.Campaigns.CloseProgramme;
using Plastoids.Modules.Sponsorship.Features.Campaigns.CreateCampaign;
using Plastoids.Modules.Sponsorship.Features.Campaigns.DownloadImpactReport;
using Plastoids.Modules.Sponsorship.Features.Campaigns.GetProgrammeBudget;
using Plastoids.Modules.Sponsorship.Features.Campaigns.GetProgrammeFunnel;
using Plastoids.Modules.Sponsorship.Features.Campaigns.PublishCampaign;
using Plastoids.Modules.Sponsorship.Features.Campaigns.ReleaseSeat;
using Plastoids.Modules.Sponsorship.Features.Campaigns.RequestQuote;
using Plastoids.Modules.Sponsorship.Features.Campaigns.ReviewCampaign;
using Plastoids.Modules.Sponsorship.Features.Opportunities.CheckEligibility;
using Plastoids.Modules.Sponsorship.Features.Opportunities.ListOpportunities;
using Plastoids.Modules.Sponsorship.Jobs;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Quartz;

namespace Plastoids.Modules.Sponsorship;

public sealed class SponsorshipModule : IModule
{
    public string Name => "sponsorship";
    public Assembly Assembly => typeof(SponsorshipModule).Assembly;
    public Type DbContextType => typeof(SponsorshipDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(ProgrammeFunded)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(ProgrammePublished)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(SeatFilled)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(SeatReleased)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(TutorEarningsAccrued)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(NotificationRequested)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ProgrammeAccess>();
        services.AddScoped<ProgrammeReader>();
        services.AddScoped<IProgrammeReader>(sp => sp.GetRequiredService<ProgrammeReader>());
        services.AddScoped<IProgrammeSeats>(sp => sp.GetRequiredService<ProgrammeReader>());
        services.AddScoped<IEligibilityChecker>(sp => sp.GetRequiredService<ProgrammeReader>());
        services.AddScoped<MonthlyFeesJob>();
        services.AddQuartz(q =>
        {
            var fees = new JobKey(MonthlyFeesJob.JobName);
            q.AddJob<MonthlyFeesJob>(j => j.WithIdentity(fees).StoreDurably());
            q.AddTrigger(t => t.ForJob(fees).WithIdentity(fees.Name + "-trigger").WithCronSchedule("0 30 2 * * ?")); // 02:30 UTC nightly
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var sponsor = endpoints.MapGroup("/sponsorship/organisations/{organisationId:guid}/programmes").WithTags("Sponsorship");
        CreateCampaignEndpoint.Map(sponsor);
        RequestQuoteEndpoint.Map(sponsor);
        ApproveQuoteEndpoint.Map(sponsor);
        GetProgrammeFunnelEndpoint.Map(sponsor);
        GetProgrammeBudgetEndpoint.Map(sponsor);
        DownloadImpactReportEndpoint.Map(sponsor);

        var opportunities = endpoints.MapGroup("/sponsorship/opportunities").WithTags("Sponsorship");
        ListOpportunitiesEndpoint.Map(opportunities);
        CheckEligibilityEndpoint.Map(opportunities);

        var admin = endpoints.MapGroup("/admin/sponsorship/programmes").WithTags("Sponsorship (staff)");
        PublishCampaignEndpoint.Map(sponsor, admin);
        ReviewCampaignEndpoint.Map(admin);
        CloseProgrammeEndpoint.Map(admin);
        ReleaseSeatEndpoint.Map(admin);
    }

    public IEnumerable<ConsumerRegistration> Consumers() =>
    [
        ConsumerRegistry.For<InvoicePaid, InvoicePaidConsumer, SponsorshipDbContext>(Name),
        ConsumerRegistry.For<OfferAccepted, OfferAcceptedConsumer, SponsorshipDbContext>(Name),
        ConsumerRegistry.For<ApplicationSubmitted, ApplicationSubmittedConsumer, SponsorshipDbContext>(Name),
        ConsumerRegistry.For<OfferSent, OfferSentConsumer, SponsorshipDbContext>(Name),
        ConsumerRegistry.For<EnrolmentCreated, EnrolmentCreatedConsumer, SponsorshipDbContext>(Name),
        ConsumerRegistry.For<EnrolmentCompleted, EnrolmentCompletedConsumer, SponsorshipDbContext>(Name),
        ConsumerRegistry.For<CertificateIssued, CertificateIssuedConsumer, SponsorshipDbContext>(Name),
        ConsumerRegistry.For<AttendanceRecorded, AttendanceRecordedConsumer, SponsorshipDbContext>(Name),
        ConsumerRegistry.For<AttemptGraded, AttemptGradedConsumer, SponsorshipDbContext>(Name),
    ];
}
