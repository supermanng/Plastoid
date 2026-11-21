using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Infrastructure.Messaging.Outbox;

/// <summary>Scoped. Handlers enqueue; the unit of work flushes to the primary context's outbox table on commit.</summary>
internal sealed class OutboxAccumulator : IOutbox
{
    private readonly List<IIntegrationEvent> _pending = [];

    public IReadOnlyList<IIntegrationEvent> Pending => _pending;

    public void Enqueue(IIntegrationEvent @event) => _pending.Add(@event);

    public void Clear() => _pending.Clear();
}
