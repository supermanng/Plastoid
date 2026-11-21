using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Messaging.Outbox;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Infrastructure.Messaging.Kafka;

/// <summary>
/// One background consumer per module. Consumer group "plastoids-&lt;module&gt;". For each message:
/// begin MySQL transaction → insert inbox row (duplicate → skip) → run handler → commit → commit offset.
/// Retries with exponential backoff up to <see cref="KafkaOptions.ConsumerMaxAttempts"/>, then dead-letters.
/// </summary>
internal sealed class KafkaConsumerHost(
    string module,
    IServiceScopeFactory scopeFactory,
    ConsumerRegistry registry,
    IKafkaProducer producer,
    IClock clock,
    IOptions<KafkaOptions> options,
    ILogger<KafkaConsumerHost> logger) : BackgroundService
{
    private readonly KafkaOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var registrations = registry.ForModule(module).ToList();
        if (registrations.Count == 0 || !_options.ConsumersEnabled)
        {
            return;
        }

        var topics = registrations.Select(r => r.Topic).Distinct().ToList();
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = $"plastoids-{module}",
            ClientId = $"{_options.ClientId}-{module}",
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky,
            AllowAutoCreateTopics = false,
        };

        // Run the blocking consume loop on its own thread.
        await Task.Factory.StartNew(() => Loop(config, topics, registrations, stoppingToken), stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    private async Task Loop(ConsumerConfig config, List<string> topics, List<ConsumerRegistration> registrations, CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => logger.LogError("Kafka consumer [{Module}] error {Code}: {Reason}", module, e.Code, e.Reason))
            .Build();

        // Wait for topics to exist (TopicProvisioner runs at startup; in tests it may race).
        consumer.Subscribe(topics);
        logger.LogInformation("Consumer [{Module}] subscribed to {Topics}", module, string.Join(", ", topics));

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result;
            try
            {
                result = consumer.Consume(TimeSpan.FromMilliseconds(250));
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Consume error [{Module}]", module);
                continue;
            }

            if (result is null || result.IsPartitionEOF)
            {
                continue;
            }

            var handled = await ProcessWithRetry(result, registrations, stoppingToken);
            if (handled)
            {
                consumer.StoreOffset(result);
                consumer.Commit(result);
            }
        }

        consumer.Close();
    }

    private async Task<bool> ProcessWithRetry(ConsumeResult<string, string> result, List<ConsumerRegistration> registrations, CancellationToken ct)
    {
        IntegrationEventEnvelope envelope;
        try
        {
            envelope = EnvelopeSerializer.ParseEnvelope(result.Message.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Poison message on {Topic} offset {Offset}; dead-lettering", result.Topic, result.Offset);
            await DeadLetter(result, ex, ct);
            return true;
        }

        var matching = registrations.Where(r => r.Topic == result.Topic).ToList();
        Exception? last = null;
        for (var attempt = 1; attempt <= _options.ConsumerMaxAttempts; attempt++)
        {
            try
            {
                await ProcessOnce(envelope, matching, ct);
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                last = ex;
                var delay = TimeSpan.FromMilliseconds(_options.ConsumerBaseBackoffMs * Math.Pow(2, attempt - 1));
                logger.LogWarning(ex, "Attempt {Attempt}/{Max} failed for {Type} {Id} on [{Module}]; retrying in {Delay}", attempt, _options.ConsumerMaxAttempts, envelope.Type, envelope.Id, module, delay);
                try
                {
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
        }

        await DeadLetter(result, last!, ct);
        return true;
    }

    private async Task ProcessOnce(IntegrationEventEnvelope envelope, List<ConsumerRegistration> matching, CancellationToken ct)
    {
        using var ambient = AmbientContext.Enter(envelope.TenantId, envelope.ActorId, envelope.CorrelationId, isSystem: false);
        await using var scope = scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var contextType = matching[0].ContextType;
        var db = (ModuleDbContext)sp.GetRequiredService(contextType);
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();

        await unitOfWork.BeginAsync(db, ct);

        var consumerName = $"{module}:{matching[0].HandlerType.Name}";
        db.Inbox.Add(new InboxMessage { MessageId = envelope.Id, Consumer = consumerName, ReceivedAt = clock.UtcNow });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            logger.LogInformation("Duplicate delivery of {Type} {Id} on [{Module}] ignored", envelope.Type, envelope.Id, module);
            await unitOfWork.RollbackAsync(ct);
            return;
        }

        foreach (var registration in matching)
        {
            var @event = EnvelopeSerializer.ParseData(envelope, registration.EventType);
            var handler = sp.GetRequiredService(registration.HandlerType);
            var method = registration.HandlerType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!;
            await (Task)method.Invoke(handler, [@event, envelope, ct])!;
        }

        var inbox = await db.Inbox.SingleAsync(i => i.MessageId == envelope.Id && i.Consumer == consumerName, ct);
        inbox.ProcessedAt = clock.UtcNow;
        await unitOfWork.CommitAsync(ct);
    }

    private async Task DeadLetter(ConsumeResult<string, string> result, Exception error, CancellationToken ct)
    {
        var headers = result.Message.Headers.ToDictionary(h => h.Key, h => System.Text.Encoding.UTF8.GetString(h.GetValueBytes()));
        headers["dlq.error"] = error.GetType().Name + ": " + error.Message;
        headers["dlq.sourceTopic"] = result.Topic;
        headers["dlq.sourceOffset"] = result.Offset.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await producer.ProduceRawAsync(result.Topic + ".dlq", result.Message.Key, result.Message.Value, headers, ct);
        logger.LogError(error, "Dead-lettered message from {Topic} offset {Offset} to {Dlq}", result.Topic, result.Offset, result.Topic + ".dlq");
    }

    private static bool IsDuplicateKey(DbUpdateException ex)
        => ex.InnerException is MySqlConnector.MySqlException { ErrorCode: MySqlConnector.MySqlErrorCode.DuplicateKeyEntry };
}
