using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Plastoids.Infrastructure.Messaging.Kafka;

/// <summary>Creates every declared topic (and its .dlq) at startup. Topics are never auto-created.</summary>
internal sealed class TopicProvisioner(
    IOptions<KafkaOptions> options,
    TopicRegistry registry,
    ILogger<TopicProvisioner> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var all = registry.Topics
            .SelectMany(t => new[] { t, t with { Name = t.Name + ".dlq" } })
            .Append(new TopicDefinition(Observability.PlastoidsHealthChecks.KafkaProbeTopic, Partitions: 1))
            .DistinctBy(t => t.Name)
            .ToList();

        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = options.Value.BootstrapServers }).Build();
        var existing = admin.GetMetadata(TimeSpan.FromSeconds(10)).Topics.Select(t => t.Topic).ToHashSet(StringComparer.Ordinal);
        var missing = all.Where(t => !existing.Contains(t.Name)).ToList();
        if (missing.Count == 0)
        {
            logger.LogInformation("Kafka topics already provisioned ({Count})", all.Count);
            return;
        }

        try
        {
            await admin.CreateTopicsAsync(missing.Select(t => new TopicSpecification
            {
                Name = t.Name,
                NumPartitions = t.Partitions ?? options.Value.DefaultPartitions,
                ReplicationFactor = options.Value.ReplicationFactor,
            }));
            logger.LogInformation("Created Kafka topics: {Topics}", string.Join(", ", missing.Select(t => t.Name)));
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code is ErrorCode.TopicAlreadyExists or ErrorCode.NoError))
        {
            logger.LogInformation("Kafka topics were created concurrently by another instance.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Declarative list of topics assembled from module registrations.</summary>
public sealed class TopicRegistry
{
    private readonly List<TopicDefinition> _topics = [];

    /// <summary>Every topic a module produces or consumes, plus audit.event.v1 which every module produces through the pipeline.</summary>
    public TopicRegistry(IEnumerable<Plastoids.Infrastructure.Modules.IModule> modules)
    {
        Declare(new TopicDefinition(EnvelopeSerializer.GetAttribute(typeof(Behaviours.AuditEvent)).Topic));
        foreach (var module in modules)
        {
            foreach (var topic in module.ProducedTopics)
            {
                Declare(topic);
            }

            foreach (var consumer in module.Consumers())
            {
                Declare(new TopicDefinition(consumer.Topic));
            }
        }
    }

    public IReadOnlyList<TopicDefinition> Topics => _topics;

    public void Declare(TopicDefinition topic)
    {
        if (_topics.All(t => t.Name != topic.Name))
        {
            _topics.Add(topic);
        }
    }
}
