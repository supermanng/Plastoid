namespace Plastoids.Infrastructure.Messaging.Kafka;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string ClientId { get; set; } = "plastoids-api";
    public int DefaultPartitions { get; set; } = 6;
    public short ReplicationFactor { get; set; } = 1;
    public int ConsumerMaxAttempts { get; set; } = 5;
    public int ConsumerBaseBackoffMs { get; set; } = 500;
    public int OutboxBatchSize { get; set; } = 200;
    public int OutboxPollIntervalMs { get; set; } = 500;

    /// <summary>Disable the consumer hosts (e.g. in a CLI run or a test that only needs the API).</summary>
    public bool ConsumersEnabled { get; set; } = true;
}

public sealed record TopicDefinition(string Name, int? Partitions = null);
