using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Plastoids.Infrastructure.Messaging.Outbox;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Infrastructure.Messaging.Kafka;

public static class EventJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };
}

/// <summary>Turns an <see cref="IIntegrationEvent"/> into an outbox row carrying the wire envelope.</summary>
public sealed class EnvelopeSerializer(IClock clock, ICurrentTenant tenant, ICurrentUser user)
{
    public OutboxMessage ToOutboxMessage(IIntegrationEvent @event, string? causationId = null)
    {
        var attribute = GetAttribute(@event.GetType());
        var id = Guid.CreateVersion7();
        var envelope = new IntegrationEventEnvelope(
            Id: id,
            Type: attribute.Type,
            Version: attribute.Version,
            OccurredAt: clock.UtcNow,
            TenantId: tenant.TenantId,
            AggregateId: @event.AggregateId,
            CorrelationId: user.CorrelationId,
            CausationId: causationId,
            ActorId: user.UserId,
            Data: JsonSerializer.Serialize(@event, @event.GetType(), EventJson.Options));

        var headers = new Dictionary<string, string>
        {
            ["type"] = envelope.Type,
            ["version"] = envelope.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["correlationId"] = envelope.CorrelationId ?? string.Empty,
            ["traceparent"] = System.Diagnostics.Activity.Current?.Id ?? string.Empty,
        };

        return new OutboxMessage
        {
            Id = id,
            Topic = attribute.Topic,
            PartitionKey = @event.AggregateId.ToString("D"),
            EventType = envelope.Type,
            EventVersion = envelope.Version,
            Payload = JsonSerializer.Serialize(envelope, EventJson.Options),
            Headers = JsonSerializer.Serialize(headers, EventJson.Options),
            OccurredAt = envelope.OccurredAt,
        };
    }

    public static IntegrationEventAttribute GetAttribute(Type eventType)
        => eventType.GetCustomAttribute<IntegrationEventAttribute>()
           ?? throw new InvalidOperationException($"{eventType.Name} must be decorated with [IntegrationEvent(module, eventName, version)].");

    public static IntegrationEventEnvelope ParseEnvelope(string json)
        => JsonSerializer.Deserialize<IntegrationEventEnvelope>(json, EventJson.Options)
           ?? throw new InvalidOperationException("Envelope payload was empty.");

    public static object ParseData(IntegrationEventEnvelope envelope, Type eventType)
        => JsonSerializer.Deserialize(envelope.Data, eventType, EventJson.Options)
           ?? throw new InvalidOperationException($"Could not deserialise {envelope.Type} as {eventType.Name}.");
}
