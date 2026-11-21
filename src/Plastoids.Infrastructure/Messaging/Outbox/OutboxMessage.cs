using Microsoft.EntityFrameworkCore;

namespace Plastoids.Infrastructure.Messaging.Outbox;

/// <summary>A row in &lt;module&gt;_outbox. Written in the business transaction, published by the OutboxPublisherJob.</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string PartitionKey { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int EventVersion { get; set; }
    public string Payload { get; set; } = string.Empty;
    public string Headers { get; set; } = "{}";
    public DateTime OccurredAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

/// <summary>A row in &lt;module&gt;_inbox: consumer-side dedup. Primary key is the envelope id.</summary>
public sealed class InboxMessage
{
    public Guid MessageId { get; set; }
    public string Consumer { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

internal static class OutboxConfiguration
{
    public static void Configure(ModelBuilder modelBuilder, string prefix)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable($"{prefix}_outbox");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("binary(16)");
            b.Property(x => x.Topic).HasMaxLength(200);
            b.Property(x => x.PartitionKey).HasMaxLength(100);
            b.Property(x => x.EventType).HasMaxLength(200);
            b.Property(x => x.Payload).HasColumnType("json");
            b.Property(x => x.Headers).HasColumnType("json");
            b.Property(x => x.OccurredAt).HasColumnType("datetime(6)");
            b.Property(x => x.PublishedAt).HasColumnType("datetime(6)");
            b.Property(x => x.LastError).HasMaxLength(2000);
            b.HasIndex(x => new { x.PublishedAt, x.OccurredAt });
        });

        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable($"{prefix}_inbox");
            b.HasKey(x => new { x.MessageId, x.Consumer });
            b.Property(x => x.MessageId).HasColumnType("binary(16)");
            b.Property(x => x.Consumer).HasMaxLength(200);
            b.Property(x => x.ReceivedAt).HasColumnType("datetime(6)");
            b.Property(x => x.ProcessedAt).HasColumnType("datetime(6)");
        });
    }
}
