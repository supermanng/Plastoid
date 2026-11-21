using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.Identity.Consumers;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Identity.Features.Diagnostics.EmitSmokeEvent;
using Plastoids.Modules.Identity.Persistence;

namespace Plastoids.Modules.Identity;

public sealed class IdentityModule : IModule
{
    public string Name => "identity";
    public Assembly Assembly => typeof(IdentityModule).Assembly;
    public Type DbContextType => typeof(IdentityDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(SmokeTestRequested)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<SmokeEventSink>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/identity").WithTags("Identity");
        if (endpoints.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<bool>("Diagnostics:Enabled"))
        {
            EmitSmokeEventEndpoint.Map(group);
        }
    }

    public IEnumerable<ConsumerRegistration> Consumers() =>
    [
        ConsumerRegistry.For<SmokeTestRequested, SmokeTestRequestedConsumer, IdentityDbContext>(Name),
    ];
}
