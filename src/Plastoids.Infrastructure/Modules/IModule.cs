using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging.Kafka;

namespace Plastoids.Infrastructure.Modules;

/// <summary>
/// Every module exposes one of these. The host discovers modules, registers their services, maps their
/// endpoints under /api/v1/&lt;prefix&gt;, wires their Kafka consumers and declares their topics.
/// </summary>
public interface IModule
{
    /// <summary>Lower-case name; also the table prefix and consumer-group suffix.</summary>
    string Name { get; }

    /// <summary>Assembly containing the module's Features (used to map commands → DbContext and to scan handlers/validators).</summary>
    Assembly Assembly { get; }

    Type DbContextType { get; }

    /// <summary>Topics this module PRODUCES (declared here so they are provisioned even before any consumer exists).</summary>
    IEnumerable<TopicDefinition> ProducedTopics { get; }

    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);

    /// <summary>Register (event → handler) pairs this module consumes.</summary>
    IEnumerable<ConsumerRegistration> Consumers();
}

/// <summary>Maps a Features assembly to its module so pipeline behaviours can find the right DbContext.</summary>
public sealed class ModuleRegistry
{
    private readonly Dictionary<Assembly, IModule> _byAssembly = [];
    private readonly List<IModule> _modules = [];

    /// <summary>Built from every registered <see cref="IModule"/>; registration order = migration order.</summary>
    public ModuleRegistry(IEnumerable<IModule> modules)
    {
        foreach (var module in modules)
        {
            _modules.Add(module);
            _byAssembly[module.Assembly] = module;
        }
    }

    public IReadOnlyList<IModule> Modules => _modules;

    public IModule ForAssembly(Assembly assembly)
        => _byAssembly.TryGetValue(assembly, out var module)
            ? module
            : throw new InvalidOperationException($"Assembly {assembly.GetName().Name} is not registered as a module.");

    public IModule ByName(string name) => _modules.Single(m => m.Name == name);
}
