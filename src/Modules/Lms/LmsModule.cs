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
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Lms.Consumers;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Lms.Features.Cohorts.CohortForum;
using Plastoids.Modules.Lms.Features.Cohorts.CreateCohort;
using Plastoids.Modules.Lms.Features.Cohorts.ListCohorts;
using Plastoids.Modules.Lms.Features.Cohorts.ScheduleCohortSessions;
using Plastoids.Modules.Lms.Features.Learning.CompleteEnrolment;
using Plastoids.Modules.Lms.Features.Learning.GetGradebook;
using Plastoids.Modules.Lms.Features.Learning.GetLessonContent;
using Plastoids.Modules.Lms.Features.Learning.GradeAssignment;
using Plastoids.Modules.Lms.Features.Learning.ListMyEnrolments;
using Plastoids.Modules.Lms.Features.Learning.QuizAttempts;
using Plastoids.Modules.Lms.Features.Learning.RequestOfflineDownload;
using Plastoids.Modules.Lms.Features.Learning.SubmitAssignment;
using Plastoids.Modules.Lms.Features.Learning.UpdateProgress;
using Plastoids.Modules.Lms.Features.Live.CreateLiveSession;
using Plastoids.Modules.Lms.Features.Live.HandleProviderAttendanceWebhook;
using Plastoids.Modules.Lms.Features.Live.IssueJoinToken;
using Plastoids.Modules.Lms.Features.Live.ListUpcomingSessions;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.Modules.Lms.Services;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;

namespace Plastoids.Modules.Lms;

public sealed class LmsModule : IModule
{
    public string Name => "lms";
    public Assembly Assembly => typeof(LmsModule).Assembly;
    public Type DbContextType => typeof(LmsDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(AttendanceRecorded)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(EnrolmentCreated)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(EnrolmentCompleted)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(AttemptGraded)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(NotificationRequested)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(OfferAccepted)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<EnrolmentService>();
        services.AddScoped<ProgressService>();
        services.AddScoped<LmsAccess>();
        services.AddScoped<TeachingAccess>();
        services.AddScoped<CohortMembership>();
        // Replaces Catalogue's stub: reviews now need a real enrolment with ≥30 % progress.
        services.AddScoped<IEnrolmentProgress, LmsEnrolmentProgress>();
        services.AddScoped<IAssessmentGrader, AssessmentGrader>();
        services.AddScoped<ICohortMembers, CohortMembers>();
        services.AddScoped<ICohortSeats, CohortSeats>();
        services.AddScoped<CohortListings>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var lms = endpoints.MapGroup("/lms").WithTags("LMS");
        HandleProviderAttendanceWebhookEndpoint.Map(lms);
        var live = lms.MapGroup("/live-sessions");
        CreateLiveSessionEndpoint.Map(live);
        IssueJoinTokenEndpoint.Map(live);
        ListUpcomingSessionsEndpoint.Map(live);

        var enrolments = lms.MapGroup("/enrolments");
        ListMyEnrolmentsEndpoint.Map(enrolments);
        GetLessonContentEndpoint.Map(enrolments);
        UpdateProgressEndpoint.Map(enrolments);
        QuizAttemptEndpoints.Map(lms);
        SubmitAssignmentEndpoint.Map(lms);
        GradeAssignmentEndpoint.Map(lms);
        GetGradebookEndpoint.Map(lms);
        CompleteEnrolmentEndpoint.Map(enrolments);
        RequestOfflineDownloadEndpoint.Map(enrolments);

        var cohorts = lms.MapGroup("/cohorts");
        ListCohortsEndpoints.Map(cohorts);
        CreateCohortEndpoint.Map(cohorts);
        ScheduleCohortSessionsEndpoint.Map(cohorts);
        CohortForumEndpoints.Map(lms);
    }

    public IEnumerable<ConsumerRegistration> Consumers() =>
    [
        ConsumerRegistry.For<BookingConfirmed, BookingConfirmedConsumer, LmsDbContext>(Name),
        ConsumerRegistry.For<BookingRescheduled, BookingRescheduledConsumer, LmsDbContext>(Name),
        ConsumerRegistry.For<BookingCancelled, BookingCancelledConsumer, LmsDbContext>(Name),
        ConsumerRegistry.For<PaymentSucceeded, CoursePaymentConsumer, LmsDbContext>(Name),
        ConsumerRegistry.For<OfferAccepted, OfferAcceptedConsumer, LmsDbContext>(Name),
        ConsumerRegistry.For<CoursePublished, CoursePublishedConsumer, LmsDbContext>(Name),
        ConsumerRegistry.For<SeatAllocated, SeatAllocatedConsumer, LmsDbContext>(Name),
    ];
}
