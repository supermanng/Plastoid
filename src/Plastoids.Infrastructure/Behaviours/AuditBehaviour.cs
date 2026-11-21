using System.Reflection;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Infrastructure.Behaviours;

/// <summary>
/// After a successful handler, snapshots Added/Modified/Deleted entities marked [Audited] from the primary
/// context and enqueues audit.event.v1 for each (consumed by the Audit module). Runs inside the unit of work.
/// </summary>
public sealed class AuditBehaviour<TRequest, TResponse>(IUnitOfWork unitOfWork, IOutbox outbox, ICurrentUser user)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);
        if (response.IsFailure || unitOfWork.Primary is null)
        {
            return response;
        }

        var db = unitOfWork.Primary;
        foreach (var entry in db.ChangeTracker.Entries<Entity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.Entity.GetType().GetCustomAttribute<AuditedAttribute>() is null)
            {
                continue;
            }

            var before = entry.State == EntityState.Added ? null : Snapshot(entry.OriginalValues);
            var after = entry.State == EntityState.Deleted ? null : Snapshot(entry.CurrentValues);
            outbox.Enqueue(new AuditEvent(
                AggregateId: entry.Entity.Id,
                ActorId: user.UserId,
                Action: $"{typeof(TRequest).Name}:{entry.State}",
                Entity: entry.Entity.GetType().Name,
                EntityId: entry.Entity.Id,
                Before: before,
                After: after,
                Ip: user.IpAddress));
        }

        return response;
    }

    private static string Snapshot(Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues values)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var p in values.Properties)
        {
            if (SensitiveColumns.Contains(p.Name))
            {
                dict[p.Name] = "[redacted]";
                continue;
            }

            dict[p.Name] = values[p];
        }

        return JsonSerializer.Serialize(dict, EventJson.Options);
    }

    private static readonly HashSet<string> SensitiveColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "Nin", "NinCiphertext", "Dob", "DobCiphertext", "Phone", "PhoneCiphertext", "Bvn", "BvnCiphertext",
    };
}

/// <summary>audit.event.v1 — produced by every module through the pipeline, consumed by the Audit module.</summary>
[IntegrationEvent("audit", "event", 1)]
public sealed record AuditEvent(
    Guid AggregateId,
    Guid? ActorId,
    string Action,
    string Entity,
    Guid EntityId,
    string? Before,
    string? After,
    string? Ip) : IIntegrationEvent;
