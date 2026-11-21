using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Audit.Domain;

/// <summary>One audit.event.v1 persisted. The table is append-only: BEFORE UPDATE/DELETE triggers reject changes.</summary>
public sealed class AuditEntry : Entity
{
    private AuditEntry()
    {
    }

    private AuditEntry(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid EventId { get; private set; }
    public Guid? ActorId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityName { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string? Before { get; private set; }
    public string? After { get; private set; }
    public string? Ip { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTime OccurredAt { get; private set; }

    public static AuditEntry From(Guid tenantId, Guid eventId, Guid? actorId, string action, string entityName, Guid entityId, string? before, string? after, string? ip, string? correlationId, DateTime occurredAt)
        => new(NewId(), tenantId)
        {
            EventId = eventId,
            ActorId = actorId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Before = before,
            After = after,
            Ip = ip,
            CorrelationId = correlationId,
            OccurredAt = occurredAt,
        };
}
