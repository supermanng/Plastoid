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
using Plastoids.Modules.Booking.Contracts;
using Plastoids.Modules.Certificates.Contracts;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Notifications.Consumers;
using Plastoids.Modules.Notifications.Contracts;
using Plastoids.Modules.Notifications.Features.Announcements.PostCohortAnnouncement;
using Plastoids.Modules.Notifications.Features.Inbox.ListMyNotifications;
using Plastoids.Modules.Notifications.Features.Preferences.ManagePreferences;
using Plastoids.Modules.Notifications.Features.Templates.UpsertTemplate;
using Plastoids.Modules.Notifications.Features.Threads.MessageThreads;
using Plastoids.Modules.Notifications.Jobs;
using Plastoids.Modules.Notifications.Persistence;
using Plastoids.Modules.Notifications.Services;
using Plastoids.Modules.Payouts.Contracts;
using Quartz;

namespace Plastoids.Modules.Notifications;

public sealed class NotificationsModule : IModule
{
    public string Name => "notifications";
    public Assembly Assembly => typeof(NotificationsModule).Assembly;
    public Type DbContextType => typeof(NotificationsDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(MessageFlagged)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(NotificationRequested)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<ChannelRouter>();
        services.AddScoped<DeliveryJob>();
        services.AddQuartz(q =>
        {
            var delivery = new JobKey(DeliveryJob.JobName);
            q.AddJob<DeliveryJob>(j => j.WithIdentity(delivery).StoreDurably());
            q.AddTrigger(t => t.ForJob(delivery).WithIdentity(delivery.Name + "-trigger").StartNow().WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(1)).RepeatForever()));
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var notifications = endpoints.MapGroup("/notifications").WithTags("Notifications");
        InboxEndpoints.Map(notifications);
        PreferencesEndpoints.Map(notifications);
        CohortAnnouncementEndpoints.Map(notifications);
        ThreadEndpoints.Map(notifications);

        var admin = endpoints.MapGroup("/admin/notifications").WithTags("Notifications (staff)");
        TemplateEndpoints.Map(admin);
    }

    public IEnumerable<ConsumerRegistration> Consumers() =>
    [
        ConsumerRegistry.For<NotificationRequested, SendRequestedConsumer, NotificationsDbContext>(Name),
        ConsumerRegistry.For<UserRegistered, UserRegisteredConsumer, NotificationsDbContext>(Name),
        ConsumerRegistry.For<BookingConfirmed, BookingConfirmedConsumer, NotificationsDbContext>(Name),
        ConsumerRegistry.For<OfferSent, OfferSentConsumer, NotificationsDbContext>(Name),
        ConsumerRegistry.For<OfferAccepted, OfferAcceptedConsumer, NotificationsDbContext>(Name),
        ConsumerRegistry.For<CertificateIssued, CertificateIssuedConsumer, NotificationsDbContext>(Name),
        ConsumerRegistry.For<PayoutSettled, PayoutSettledConsumer, NotificationsDbContext>(Name),
        ConsumerRegistry.For<EnrolmentCreated, EnrolmentCreatedConsumer, NotificationsDbContext>(Name),
    ];
}
