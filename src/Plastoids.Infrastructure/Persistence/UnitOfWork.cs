using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Infrastructure.Persistence;

/// <summary>
/// One MySQL transaction per command (or per consumed message). All module contexts in the scope share the
/// <see cref="ScopedDbConnection"/>; contexts enlisted here join the same transaction, so business rows,
/// ledger postings and outbox rows commit atomically.
/// </summary>
public interface IUnitOfWork
{
    bool IsActive { get; }
    ModuleDbContext? Primary { get; }
    Task BeginAsync(ModuleDbContext primary, CancellationToken cancellationToken);
    Task EnlistAsync(DbContext context, CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}

internal sealed class UnitOfWork(IOutbox outbox, EnvelopeSerializer serializer) : IUnitOfWork, IAsyncDisposable
{
    private readonly List<DbContext> _enlisted = [];
    private IDbContextTransaction? _transaction;

    public bool IsActive => _transaction is not null;
    public ModuleDbContext? Primary { get; private set; }

    public async Task BeginAsync(ModuleDbContext primary, CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A unit of work is already active in this scope.");
        }

        Primary = primary;
        _transaction = await primary.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        _enlisted.Add(primary);
    }

    public async Task EnlistAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("No active unit of work.");
        }

        if (_enlisted.Contains(context))
        {
            return;
        }

        await context.Database.UseTransactionAsync(_transaction.GetDbTransaction(), cancellationToken);
        _enlisted.Add(context);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null || Primary is null)
        {
            throw new InvalidOperationException("No active unit of work.");
        }

        // Flush integration events raised during the command to the primary module's outbox.
        foreach (var @event in outbox.Pending)
        {
            Primary.Outbox.Add(serializer.ToOutboxMessage(@event));
        }

        outbox.Clear();

        foreach (var context in _enlisted)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
        _enlisted.Clear();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        outbox.Clear();
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
        _enlisted.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
        }
    }
}
