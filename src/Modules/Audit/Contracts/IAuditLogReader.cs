namespace Plastoids.Modules.Audit.Contracts;

public sealed record AuditLogEntry(Guid Id, DateTime OccurredAt, Guid? ActorId, string Action, string Entity, Guid EntityId, string? Before, string? After, string? Ip, string? CorrelationId);

public sealed record AuditLogPage(IReadOnlyList<AuditLogEntry> Items, string? NextCursor);

/// <summary>Read-only access to the immutable audit log for the back office (blueprint §9.20 audit log viewer).</summary>
public interface IAuditLogReader
{
    Task<AuditLogPage> ListAsync(string? cursor, int? limit, Guid? actorId, string? entity, Guid? entityId, DateTime? from, DateTime? to, CancellationToken cancellationToken);
}
