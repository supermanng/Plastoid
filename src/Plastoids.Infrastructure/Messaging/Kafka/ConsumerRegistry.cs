using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Infrastructure.Messaging.Kafka;

/// <summary>One registration = one (module, topic, event type, handler). The consumer host for the module subscribes to all its topics.</summary>
public sealed record ConsumerRegistration(string Module, Type ContextType, string Topic, Type EventType, Type HandlerType);

public sealed class ConsumerRegistry
{
    private readonly List<ConsumerRegistration> _registrations = [];

    public ConsumerRegistry(IEnumerable<Plastoids.Infrastructure.Modules.IModule> modules)
    {
        foreach (var module in modules)
        {
            _registrations.AddRange(module.Consumers());
        }
    }

    public IReadOnlyList<ConsumerRegistration> Registrations => _registrations;

    public IEnumerable<ConsumerRegistration> ForModule(string module)
        => _registrations.Where(r => r.Module == module);

    public static ConsumerRegistration For<TEvent, THandler, TContext>(string module)
        where TEvent : IIntegrationEvent
        where THandler : IIntegrationEventHandler<TEvent>
    {
        var attribute = EnvelopeSerializer.GetAttribute(typeof(TEvent));
        return new ConsumerRegistration(module, typeof(TContext), attribute.Topic, typeof(TEvent), typeof(THandler));
    }
}
