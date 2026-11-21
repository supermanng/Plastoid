using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Messaging.Outbox;

namespace Plastoids.Infrastructure.Messaging.Kafka;

/// <summary>The only Kafka producer in the system. Used by the outbox publisher and the DLQ path, nothing else.</summary>
public interface IKafkaProducer
{
    Task ProduceAsync(OutboxMessage message, CancellationToken cancellationToken);
    Task ProduceRawAsync(string topic, string key, string payload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken);
}

internal sealed class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IOptions<KafkaOptions> options, ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            ClientId = options.Value.ClientId,
            EnableIdempotence = true,
            Acks = Acks.All,
            MaxInFlight = 5,
            MessageSendMaxRetries = int.MaxValue,
            RetryBackoffMs = 200,
            LingerMs = 5,
            CompressionType = CompressionType.Lz4,
        };
        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka producer error {Code}: {Reason}", e.Code, e.Reason))
            .Build();
    }

    public Task ProduceAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var headers = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(message.Headers, EventJson.Options) ?? [];
        return ProduceRawAsync(message.Topic, message.PartitionKey, message.Payload, headers, cancellationToken);
    }

    public async Task ProduceRawAsync(string topic, string key, string payload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        var kafkaHeaders = new Headers();
        foreach (var (k, v) in headers)
        {
            kafkaHeaders.Add(k, System.Text.Encoding.UTF8.GetBytes(v));
        }

        var result = await _producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = payload, Headers = kafkaHeaders }, cancellationToken);
        if (result.Status != PersistenceStatus.Persisted)
        {
            throw new KafkaException(new Error(ErrorCode.Local_Fail, $"Message to {topic} not persisted: {result.Status}"));
        }
    }

    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
        }
        finally
        {
            _producer.Dispose();
        }
    }
}
