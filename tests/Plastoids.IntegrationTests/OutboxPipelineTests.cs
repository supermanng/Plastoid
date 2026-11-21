using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Identity.Consumers;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.IntegrationTests;

[Collection(ApiCollection.Name)]
public class OutboxPipelineTests(PlastoidsApiFactory factory)
{
    [Fact]
    public async Task Health_ready_reports_mysql_kafka_and_redis()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Validation_failure_is_a_422_problem_with_field_errors()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/identity/diagnostics/smoke", new { note = "" });
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("validation_failed").And.Contain("Note");
    }

    [Fact]
    public async Task Command_writes_outbox_row_which_reaches_Kafka_and_is_consumed_exactly_once_under_redelivery()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/identity/diagnostics/smoke", new { note = "hello" });
        response.StatusCode.Should().Be(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
        var accepted = await response.Content.ReadFromJsonAsync<SmokeResponse>();
        accepted.Should().NotBeNull();
        var aggregateId = accepted!.AggregateId;

        var sink = factory.Services.GetRequiredService<SmokeEventSink>();

        // 1. The consumer sees it once.
        await WaitUntil(() => sink.CountFor(aggregateId) >= 1, TimeSpan.FromSeconds(60), "consumer to receive the event");
        sink.CountFor(aggregateId).Should().Be(1);

        // 2. The outbox row was marked published and an inbox row exists.
        string payload;
        string key;
        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var outbox = await db.Outbox.SingleAsync(m => m.PartitionKey == aggregateId.ToString("D"));
            outbox.PublishedAt.Should().NotBeNull();
            outbox.Topic.Should().Be("plastoids.identity.smoke-test-requested.v1");
            payload = outbox.Payload;
            key = outbox.PartitionKey;

            (await db.Inbox.CountAsync(i => i.MessageId == outbox.Id)).Should().Be(1);
        }

        // 3. Redeliver the very same envelope: inbox dedup must swallow it.
        var producer = factory.Services.GetRequiredService<IKafkaProducer>();
        await producer.ProduceRawAsync("plastoids.identity.smoke-test-requested.v1", key, payload, new Dictionary<string, string>(), CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(3));
        sink.CountFor(aggregateId).Should().Be(1, "a redelivered message must be ignored by the inbox");
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout, string what)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }

            await Task.Delay(200);
        }
    }

    private sealed record SmokeResponse(Guid AggregateId);
}
