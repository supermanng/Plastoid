namespace Plastoids.SharedKernel.Messaging;

/// <summary>
/// A fact published to Kafka through the transactional outbox. Lives in a module's Contracts namespace.
/// Decorate with <see cref="IntegrationEventAttribute"/> to bind it to a topic.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Partition key: the aggregate id, so events for one aggregate stay ordered.</summary>
    Guid AggregateId { get; }
}

/// <summary>
/// Binds an event type to its Kafka topic. Topic name is "plastoids.&lt;module&gt;.&lt;event-name&gt;.v&lt;major&gt;".
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventAttribute(string module, string eventName, int version = 1) : Attribute
{
    public string Module { get; } = module;
    public string EventName { get; } = eventName;
    public int Version { get; } = version;

    /// <summary>Logical type, e.g. "payments.payment-succeeded".</summary>
    public string Type => $"{Module}.{EventName}";

    public string Topic => $"plastoids.{Module}.{EventName}.v{Version}";
}

/// <summary>The wire envelope (blueprint build prompt §2.7). Headers duplicate type, version, correlationId and traceparent.</summary>
public sealed record IntegrationEventEnvelope(
    Guid Id,
    string Type,
    int Version,
    DateTime OccurredAt,
    Guid TenantId,
    Guid AggregateId,
    string? CorrelationId,
    string? CausationId,
    Guid? ActorId,
    string Data);

/// <summary>Handler for one integration event type, owned by the consuming module (Consumers/ folder).</summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken);
}

/// <summary>
/// Collects events raised during a command; the unit of work writes them to the module's outbox table
/// in the same transaction as the business change. Handlers never talk to Kafka directly.
/// </summary>
public interface IOutbox
{
    void Enqueue(IIntegrationEvent @event);
    IReadOnlyList<IIntegrationEvent> Pending { get; }
    void Clear();
}
