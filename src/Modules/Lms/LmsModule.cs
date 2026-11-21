using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.Booking.Contracts;
using Plastoids.Modules.Lms.Consumers;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Lms.Features.Live.CreateLiveSession;
using Plastoids.Modules.Lms.Features.Live.HandleProviderAttendanceWebhook;
using Plastoids.Modules.Lms.Features.Live.IssueJoinToken;
using Plastoids.Modules.Lms.Features.Live.ListUpcomingSessions;
using Plastoids.Modules.Lms.Persistence;

namespace Plastoids.Modules.Lms;

/// <summary>Sprint 4 brings the live-session part of the LMS; lessons, quizzes and enrolments follow in Sprint 5.</summary>
public sealed class LmsModule : IModule
{
    public string Name => "lms";
    public Assembly Assembly => typeof(LmsModule).Assembly;
    public Type DbContextType => typeof(LmsDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(AttendanceRecorded)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var lms = endpoints.MapGroup("/lms").WithTags("LMS");
        HandleProviderAttendanceWebhookEndpoint.Map(lms);
        var live = lms.MapGroup("/live-sessions");
        CreateLiveSessionEndpoint.Map(live);
        IssueJoinTokenEndpoint.Map(live);
        ListUpcomingSessionsEndpoint.Map(live);
    }

    public IEnumerable<ConsumerRegistration> Consumers() =>
    [
        ConsumerRegistry.For<BookingConfirmed, BookingConfirmedConsumer, LmsDbContext>(Name),
        ConsumerRegistry.For<BookingRescheduled, BookingRescheduledConsumer, LmsDbContext>(Name),
        ConsumerRegistry.For<BookingCancelled, BookingCancelledConsumer, LmsDbContext>(Name),
    ];
}
