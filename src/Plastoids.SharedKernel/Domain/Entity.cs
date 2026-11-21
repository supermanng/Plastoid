namespace Plastoids.SharedKernel.Domain;

/// <summary>
/// Base for every persisted aggregate/entity. Every table carries these columns (blueprint §16).
/// Ids are UUID v7 (time-ordered) stored as BINARY(16).
/// </summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected Entity(Guid id, Guid tenantId)
    {
        Id = id;
        TenantId = tenantId;
    }

    /// <summary>EF Core materialisation constructor.</summary>
    protected Entity()
    {
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token, incremented by the persistence layer on every update (MySQL has no rowversion).</summary>
    public ulong RowVersion { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public static Guid NewId() => Guid.CreateVersion7();
}

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}

public abstract record DomainEvent(DateTime OccurredAt) : IDomainEvent;

/// <summary>Marks an entity whose Added/Modified/Deleted changes are captured as audit.event.v1 with before/after snapshots.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class AuditedAttribute : Attribute
{
}

/// <summary>Soft-deletable user-facing record (blueprint §16: soft delete for user-facing records).</summary>
public interface ISoftDeletable
{
    DateTime? DeletedAt { get; }
}
