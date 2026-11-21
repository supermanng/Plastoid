using System.Collections.Concurrent;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Identity.Consumers;

/// <summary>Records received smoke events so tests can assert exactly-once processing under redelivery.</summary>
public sealed class SmokeEventSink
{
    private readonly ConcurrentDictionary<Guid, int> _seen = new();

    public int CountFor(Guid aggregateId) => _seen.TryGetValue(aggregateId, out var n) ? n : 0;

    internal void Record(Guid aggregateId) => _seen.AddOrUpdate(aggregateId, 1, (_, n) => n + 1);
}

internal sealed class SmokeTestRequestedConsumer(SmokeEventSink sink) : IIntegrationEventHandler<SmokeTestRequested>
{
    public Task HandleAsync(SmokeTestRequested @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        sink.Record(@event.AggregateId);
        return Task.CompletedTask;
    }
}
