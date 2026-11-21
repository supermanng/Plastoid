using Plastoids.Infrastructure.Behaviours;
using Plastoids.Modules.Audit.Domain;
using Plastoids.Modules.Audit.Persistence;
using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Audit.Consumers;

/// <summary>Writes every audit.event.v1 to audit_events. Runs inside the consumer host's transaction with inbox dedup.</summary>
internal sealed class AuditEventConsumer(AuditDbContext db) : IIntegrationEventHandler<AuditEvent>
{
    public Task HandleAsync(AuditEvent @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        db.Events.Add(AuditEntry.From(
            envelope.TenantId,
            envelope.Id,
            @event.ActorId,
            @event.Action,
            @event.Entity,
            @event.EntityId,
            @event.Before,
            @event.After,
            @event.Ip,
            envelope.CorrelationId,
            envelope.OccurredAt));
        return Task.CompletedTask;
    }
}
