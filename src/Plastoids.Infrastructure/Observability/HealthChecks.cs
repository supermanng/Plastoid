using System.Text.Json;
using Confluent.Kafka;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Plastoids.Infrastructure.Observability;

public static class PlastoidsHealthChecks
{
    public const string ReadyTag = "ready";

    /// <summary>Topic the Kafka readiness probe produces to. Provisioned by <see cref="Messaging.Kafka.TopicProvisioner"/>;
    /// topics are never auto-created.</summary>
    public const string KafkaProbeTopic = "plastoids.healthcheck";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>MySQL, Redis and Kafka readiness checks. Live checks have no dependencies.</summary>
    public static IServiceCollection AddPlastoidsHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var mysql = configuration.GetConnectionString(InfrastructureServiceCollectionExtensions.ConnectionStringName)
            ?? throw new InvalidOperationException("ConnectionStrings:Plastoids is not configured.");
        services.AddHealthChecks()
            .AddMySql(InfrastructureServiceCollectionExtensions.EnsureGuidFormat(mysql), name: "mysql", tags: [ReadyTag])
            .AddRedis(configuration.GetConnectionString("Redis") ?? "localhost:6379", name: "redis", tags: [ReadyTag])
            .AddKafka(new ProducerConfig { BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092" }, topic: KafkaProbeTopic, name: "kafka", tags: [ReadyTag]);
        return services;
    }

    /// <summary>Writes the overall status plus one entry per check so an unhealthy probe says which dependency failed.</summary>
    public static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = new
        {
            status = report.Status.ToString(),
            totalDurationMs = (long)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = (long)e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                error = e.Value.Exception?.Message,
            }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions), context.RequestAborted);
    }
}
