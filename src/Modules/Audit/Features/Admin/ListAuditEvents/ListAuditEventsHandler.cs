using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Audit.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Audit.Features.Admin.ListAuditEvents;

internal sealed class ListAuditEventsHandler(AuditDbContext db) : IQueryHandler<ListAuditEventsQuery, Page<AuditEventItem>>
{
    public async Task<Result<Page<AuditEventItem>>> Handle(ListAuditEventsQuery request, CancellationToken cancellationToken)
    {
        var limit = Cursor.ClampLimit(request.Limit);
        var query = db.Events.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Entity))
        {
            query = query.Where(e => e.EntityName == request.Entity);
        }

        if (request.EntityId is { } entityId)
        {
            query = query.Where(e => e.EntityId == entityId);
        }

        if (request.ActorId is { } actorId)
        {
            query = query.Where(e => e.ActorId == actorId);
        }

        if (Cursor.Decode(request.Cursor) is { } after)
        {
            // Ids are UUID v7: ordering by id descending is ordering by insertion time.
            query = query.Where(e => e.Id.CompareTo(after) < 0);
        }

        var rows = await query.OrderByDescending(e => e.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;
        var items = rows.Take(limit)
            .Select(e => new AuditEventItem(e.Id, e.OccurredAt, e.ActorId, e.Action, e.EntityName, e.EntityId, e.Before, e.After, e.Ip, e.CorrelationId))
            .ToList();
        return new Page<AuditEventItem>(items, hasMore ? Cursor.Encode(items[^1].Id) : null);
    }
}
