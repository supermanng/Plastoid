using Plastoids.Infrastructure.Http;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Audit.Features.Admin.ListAuditEvents;

/// <summary>Newest first, cursor-paginated (blueprint §17). Filters are optional.</summary>
public sealed record ListAuditEventsQuery(string? Cursor, int? Limit, string? Entity, Guid? EntityId, Guid? ActorId) : IQuery<Page<AuditEventItem>>;

public sealed record AuditEventItem(
    Guid Id,
    DateTime OccurredAt,
    Guid? ActorId,
    string Action,
    string Entity,
    Guid EntityId,
    string? Before,
    string? After,
    string? Ip,
    string? CorrelationId);
