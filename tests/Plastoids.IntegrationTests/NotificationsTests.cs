using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Modules.Notifications.Jobs;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

[Collection(ApiCollection.Name)]
public class NotificationsTests(PlastoidsApiFactory factory)
{
    [Fact]
    public async Task Registration_lands_in_the_inbox_and_quiet_hours_defer_external_delivery()
    {
        var (learner, signIn) = await factory.SignInWithPhoneAsync(NewPhone(), "Inbox Learner");
        JsonElement inbox = default;
        await WaitUntilAsync(async () =>
        {
            inbox = await learner.GetFromJsonAsync<JsonElement>("/api/v1/notifications/me", Json);
            return inbox.GetProperty("items").EnumerateArray().Any(i => i.GetProperty("templateKey").GetString() == "identity.user.registered");
        }, TimeSpan.FromSeconds(60), "welcome notification from identity.user-registered");
        var welcome = inbox.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("templateKey").GetString() == "identity.user.registered");
        welcome.GetProperty("body").GetString().Should().Contain("Welcome Inbox Learner!");
        welcome.GetProperty("deliveries").EnumerateArray().Should().ContainSingle(d => d.GetProperty("channel").GetString() == "push", "the default channel order starts with push and the learner has no email");

        // The delivery job sends queued messages through the fake channels.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DeliveryJob>().RunAsync(DateTime.UtcNow, CancellationToken.None);
        }

        factory.Services.GetRequiredService<FakeMessageChannels>().To(signIn.UserId).Should().Contain(m => m.Channel == "push" && m.Body.Contains("Welcome"));

        var read = await learner.PostAsync($"/api/v1/notifications/{welcome.GetProperty("notificationId").GetGuid()}/read", null);
        read.StatusCode.Should().Be(HttpStatusCode.OK, await read.Content.ReadAsStringAsync());
        (await read.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("readAt").ValueKind.Should().Be(JsonValueKind.String);
        (await learner.GetFromJsonAsync<JsonElement>("/api/v1/notifications/me?unreadOnly=true", Json)).GetProperty("items").EnumerateArray().Should().NotContain(i => i.GetProperty("templateKey").GetString() == "identity.user.registered");

        // Quiet hours covering "now" (Lagos) → SMS preferred, deferred until the window ends.
        var lagosNow = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(1));
        var prefs = await learner.PutAsJsonAsync("/api/v1/notifications/preferences", new { channelOrder = new[] { "sms", "push" }, quietStart = lagosNow.AddHours(-1).ToString("HH:mm"), quietEnd = lagosNow.AddHours(2).ToString("HH:mm"), timeZone = "Africa/Lagos" });
        prefs.StatusCode.Should().Be(HttpStatusCode.OK, await prefs.Content.ReadAsStringAsync());
        (await prefs.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("quietNow").GetBoolean().Should().BeTrue();

        // A tutor opens a thread with the learner: the learner's notification is created (send-requested path) but its SMS waits.
        var (tutor, tutorSignIn, _) = await CatalogueTests.VerifiedTutorAsync(factory);
        var opened = await tutor.PostAsJsonAsync("/api/v1/notifications/threads", new { otherUserId = signIn.UserId, subject = "Your SQL homework", body = "Let's go through question 3 on Friday." });
        opened.StatusCode.Should().Be(HttpStatusCode.Created, await opened.Content.ReadAsStringAsync());
        var threadId = (await opened.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("threadId").GetGuid();
        JsonElement item = default;
        await WaitUntilAsync(async () =>
        {
            var page = await learner.GetFromJsonAsync<JsonElement>("/api/v1/notifications/me", Json);
            item = page.GetProperty("items").EnumerateArray().FirstOrDefault(i => i.GetProperty("templateKey").GetString() == "notifications.thread.message");
            return item.ValueKind == JsonValueKind.Object;
        }, TimeSpan.FromSeconds(60), "thread message notification");
        var delivery = item.GetProperty("deliveries").EnumerateArray().Single();
        delivery.GetProperty("channel").GetString().Should().Be("sms");
        delivery.GetProperty("status").GetString().Should().Be("deferred");
        delivery.GetProperty("deferredUntil").ValueKind.Should().Be(JsonValueKind.String);

        // Learner replies with a phone number: kept, flagged; the tutor sees the flag.
        var flagged = await learner.PostAsJsonAsync($"/api/v1/notifications/threads/{threadId}/messages", new { body = "Call me on 08031234567 instead" });
        flagged.StatusCode.Should().Be(HttpStatusCode.Created, await flagged.Content.ReadAsStringAsync());
        var message = await flagged.Content.ReadFromJsonAsync<JsonElement>(Json);
        message.GetProperty("flagged").GetBoolean().Should().BeTrue();
        message.GetProperty("flagReason").GetString().Should().Be("contact_details");
        var detail = await tutor.GetFromJsonAsync<JsonElement>($"/api/v1/notifications/threads/{threadId}", Json);
        detail.GetProperty("thread").GetProperty("flagged").GetBoolean().Should().BeTrue();
        detail.GetProperty("messages").GetArrayLength().Should().Be(2);
        var (outsider, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Outsider");
        (await outsider.GetAsync($"/api/v1/notifications/threads/{threadId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Approved template versions override the built-in copy for later messages.
        var staff = await factory.SignInAsStaffAsync();
        staff.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var template = await staff.PostAsJsonAsync("/api/v1/admin/notifications/templates", new { key = "notifications.thread.message", channel = "inapp", subject = "Message from {{senderName}}", body = "{{senderName}} wrote: {{preview}}", approve = true });
        template.StatusCode.Should().Be(HttpStatusCode.Created, await template.Content.ReadAsStringAsync());
        (await template.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("version").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        _ = tutorSignIn;
        tutor.Dispose();
    }
}
