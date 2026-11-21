using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;
using Quartz;

namespace Plastoids.Infrastructure.Jobs;

/// <summary>
/// Polls &lt;module&gt;_outbox every ~500 ms: SELECT … FOR UPDATE SKIP LOCKED, produce, mark published.
/// Idempotent producer + inbox dedup on the consumer side make at-least-once safe.
/// </summary>
[DisallowConcurrentExecution]
public sealed class OutboxPublisherJob<TContext>(
    TContext db,
    IKafkaProducer producer,
    IClock clock,
    IOptions<KafkaOptions> options,
    ILogger<OutboxPublisherJob<TContext>> logger) : IJob
    where TContext : ModuleDbContext
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        var table = $"{db.ModulePrefix}_outbox";
        var batch = options.Value.OutboxBatchSize;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
#pragma warning disable EF1002 // table name comes from a trusted constant, not user input
        var pending = await db.Outbox
            .FromSqlRaw($"SELECT * FROM `{table}` WHERE published_at IS NULL AND attempts < 10 ORDER BY occurred_at LIMIT {batch} FOR UPDATE SKIP LOCKED")
            .ToListAsync(ct);
#pragma warning restore EF1002

        if (pending.Count == 0)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        var published = 0;
        foreach (var message in pending)
        {
            try
            {
                await producer.ProduceAsync(message, ct);
                message.PublishedAt = clock.UtcNow;
                message.LastError = null;
                published++;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                logger.LogError(ex, "Outbox publish failed for {Id} to {Topic} (attempt {Attempts})", message.Id, message.Topic, message.Attempts);
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogDebug("Outbox [{Prefix}] published {Published}/{Total}", db.ModulePrefix, published, pending.Count);
    }
}

/// <summary>Nightly purge of published outbox rows older than 7 days.</summary>
[DisallowConcurrentExecution]
public sealed class OutboxPurgeJob<TContext>(TContext db, IClock clock) : IJob
    where TContext : ModuleDbContext
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        var cutoff = clock.UtcNow.AddDays(-7);
        await db.Outbox.Where(m => m.PublishedAt != null && m.PublishedAt < cutoff).ExecuteDeleteAsync(context.CancellationToken);
        await db.Inbox.Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff).ExecuteDeleteAsync(context.CancellationToken);
    }
}
