using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Behaviours;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.Audit.Consumers;
using Plastoids.Modules.Audit.Features.Admin.ListAuditEvents;
using Plastoids.Modules.Audit.Persistence;

namespace Plastoids.Modules.Audit;

/// <summary>Consumes audit.event.v1 from every module into an append-only table and exposes it to staff.</summary>
public sealed class AuditModule : IModule
{
    public string Name => "audit";
    public Assembly Assembly => typeof(AuditModule).Assembly;
    public Type DbContextType => typeof(AuditDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics => [];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin").WithTags("Audit (staff)");
        ListAuditEventsEndpoint.Map(admin);
    }

    public IEnumerable<ConsumerRegistration> Consumers() =>
    [
        ConsumerRegistry.For<AuditEvent, AuditEventConsumer, AuditDbContext>(Name),
    ];
}
