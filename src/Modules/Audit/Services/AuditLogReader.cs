using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Audit.Contracts;
using Plastoids.Modules.Audit.Persistence;

namespace Plastoids.Modules.Audit.Services;

internal sealed class AuditLogReader(AuditDbContext db, IUnitOfWork unitOfWork) : IAuditLogReader
{
    public async Task<AuditLogPage> ListAsync(string? cursor, int? limit, Guid? actorId, string? entity, Guid? entityId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        var size = Cursor.ClampLimit(limit);
        var query = db.Events.AsNoTracking().AsQueryable();
        if (actorId is { } a)
        {
            query = query.Where(e => e.ActorId == a);
        }

        if (!string.IsNullOrWhiteSpace(entity))
        {
            query = query.Where(e => e.EntityName == entity);
        }

        if (entityId is { } id)
        {
            query = query.Where(e => e.EntityId == id);
        }

        if (from is { } f)
        {
            query = query.Where(e => e.OccurredAt >= f);
        }

        if (to is { } t)
        {
            query = query.Where(e => e.OccurredAt < t);
        }

        if (Cursor.Decode(cursor) is { } after)
        {
            query = query.Where(e => e.Id.CompareTo(after) < 0);
        }

        var rows = await query.OrderByDescending(e => e.Id).Take(size + 1).ToListAsync(cancellationToken);
        var items = rows.Take(size).Select(e => new AuditLogEntry(e.Id, e.OccurredAt, e.ActorId, e.Action, e.EntityName, e.EntityId, e.Before, e.After, e.Ip, e.CorrelationId)).ToList();
        return new AuditLogPage(items, rows.Count > size ? Cursor.Encode(items[^1].Id) : null);
    }
}
