using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.BackOffice.Consumers;
using Plastoids.Modules.BackOffice.Contracts;
using Plastoids.Modules.BackOffice.Features.AuditLog.ViewAuditLog;
using Plastoids.Modules.BackOffice.Features.Dashboard.GetDashboard;
using Plastoids.Modules.BackOffice.Features.FeatureFlags.ManageFeatureFlags;
using Plastoids.Modules.BackOffice.Features.Queues.ListQueues;
using Plastoids.Modules.BackOffice.Features.Users.ManageUsers;
using Plastoids.Modules.BackOffice.Persistence;
using Plastoids.Modules.BackOffice.Services;
using Plastoids.Modules.Booking.Contracts;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payouts.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.Modules.Tutors.Contracts;

namespace Plastoids.Modules.BackOffice;

/// <summary>Super Admin and back office (blueprint §9.20, story I1): dashboard, queues, user management, fee rules, flags, audit viewer. All under /admin/backoffice/*.</summary>
public sealed class BackOfficeModule : IModule
{
    public string Name => "backoffice";
    public Assembly Assembly => typeof(BackOfficeModule).Assembly;
    public Type DbContextType => typeof(BackOfficeDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics => [];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IFeatureFlags, FeatureFlags>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/backoffice").WithTags("Back office (staff)");
        GetDashboardEndpoint.Map(admin);
        ManageUsersEndpoints.Map(admin);
        QueueEndpoints.Map(admin);
        FeatureFlagEndpoints.Map(admin);
        ViewAuditLogEndpoints.Map(admin);
    }

    public IEnumerable<ConsumerRegistration> Consumers() =>
    [
        ConsumerRegistry.For<PaymentSucceeded, PaymentSucceededProjector, BackOfficeDbContext>(Name),
        ConsumerRegistry.For<RefundIssued, RefundIssuedProjector, BackOfficeDbContext>(Name),
        ConsumerRegistry.For<UserRegistered, UserRegisteredProjector, BackOfficeDbContext>(Name),
        ConsumerRegistry.For<OrganisationVerified, OrganisationVerifiedProjector, BackOfficeDbContext>(Name),
        ConsumerRegistry.For<TutorVerified, TutorVerifiedProjector, BackOfficeDbContext>(Name),
        ConsumerRegistry.For<EnrolmentCreated, EnrolmentCreatedProjector, BackOfficeDbContext>(Name),
        ConsumerRegistry.For<EnrolmentCompleted, EnrolmentCompletedProjector, BackOfficeDbContext>(Name),
        ConsumerRegistry.For<ProgrammeFunded, ProgrammeFundedProjector, BackOfficeDbContext>(Name),
        ConsumerRegistry.For<ProgrammePublished, ProgrammePublishedProjector, BackOfficeDbContext>(Name),
        ConsumerRegistry.For<BatchApproved, BatchApprovedProjector, BackOfficeDbContext>(Name),
        ConsumerRegistry.For<PayoutSettled, PayoutSettledProjector, BackOfficeDbContext>(Name),
        ConsumerRegistry.For<PayoutFailed, PayoutFailedProjector, BackOfficeDbContext>(Name),
        ConsumerRegistry.For<ShortfallDetected, ShortfallProjector, BackOfficeDbContext>(Name),
        ConsumerRegistry.For<TutorStrikeRecorded, TutorStrikeProjector, BackOfficeDbContext>(Name),
    ];
}
