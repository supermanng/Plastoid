using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Booking.Jobs;
using Plastoids.Modules.Booking.Persistence;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

[Collection(ApiCollection.Name)]
public class BookingAndLiveSessionTests(PlastoidsApiFactory factory)
{
    /// <summary>A verified tutor available every day, all day (UTC), at ₦8,000/hour.</summary>
    private static async Task<(HttpClient Tutor, Guid TutorUserId, Guid TutorId)> AvailableTutorAsync(PlastoidsApiFactory factory)
    {
        var (tutor, userId, tutorId) = await CatalogueTests.VerifiedTutorAsync(factory);
        var weekly = Enum.GetValues<DayOfWeek>().Select(d => new { day = d.ToString().ToLowerInvariant(), start = "00:00", end = "23:59" }).ToArray();
        (await tutor.PutAsJsonAsync("/api/v1/tutors/me/availability", new { timeZone = "UTC", weekly, exceptions = Array.Empty<object>() })).IsSuccessStatusCode.Should().BeTrue();
        (await tutor.PutAsJsonAsync("/api/v1/tutors/me/pricing", new { hourlyRate = new { amount = 800000, currency = "NGN" }, rules = Array.Empty<object>() })).IsSuccessStatusCode.Should().BeTrue();
        return (tutor, userId, tutorId);
    }

    private static DateTime NextSlot(int hoursAhead)
    {
        var t = DateTime.UtcNow.AddHours(hoursAhead);
        return new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0, DateTimeKind.Utc);
    }

    private async Task<JsonElement> ConfirmByWebhookAsync(string gatewayReference, long amountKobo)
    {
        var gateway = factory.Services.GetRequiredService<FakePaymentGateway>();
        var (payload, signature) = gateway.BuildWebhook("charge.success", gatewayReference, Money.Kobo(amountKobo), DateTime.UtcNow);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/fake") { Content = new StringContent(payload, Encoding.UTF8, "application/json"), Headers = { { "x-paystack-signature", signature } } };
        var response = await factory.CreateClient().SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    [Fact]
    public async Task Lock_pay_confirm_join_and_attend_a_session_with_lock_contention_and_double_booking_guard()
    {
        var (tutor, tutorUserId, tutorId) = await AvailableTutorAsync(factory);
        var (learner, learnerSignIn) = await factory.SignInWithPhoneAsync(NewPhone(), "Session learner");
        var (rival, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Rival learner");
        var slot = NextSlot(48);

        var quote = await learner.GetFromJsonAsync<JsonElement>($"/api/v1/bookings/quote?tutorId={tutorId}&slotStart={slot:O}&durationMinutes=60&format=one_to_one", Json);
        quote.GetProperty("price").GetProperty("amount").GetInt64().Should().Be(800000);

        // Slot lock contention: the rival cannot lock what the learner holds.
        var lockBody = new { tutorId, slotStart = slot, durationMinutes = 60, format = "one_to_one" };
        (await learner.PostAsJsonAsync("/api/v1/bookings/locks", lockBody)).StatusCode.Should().Be(HttpStatusCode.OK);
        var contended = await rival.PostAsJsonAsync("/api/v1/bookings/locks", lockBody);
        contended.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await contended.Content.ReadAsStringAsync()).Should().Contain("slot_locked");
        rival.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var rivalCreate = await rival.PostAsJsonAsync("/api/v1/bookings", new { tutorId, slotStart = slot, durationMinutes = 60, format = "one_to_one" });
        rivalCreate.StatusCode.Should().Be(HttpStatusCode.Conflict, await rivalCreate.Content.ReadAsStringAsync());
        rival.DefaultRequestHeaders.Remove("Idempotency-Key");

        learner.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var created = await learner.PostAsJsonAsync("/api/v1/bookings", new { tutorId, slotStart = slot, durationMinutes = 60, format = "one_to_one", note = "SQL joins please" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var booking = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        var bookingId = booking.GetProperty("bookingId").GetGuid();
        booking.GetProperty("status").GetString().Should().Be("pendingpayment");
        booking.GetProperty("checkoutUrl").GetString().Should().Contain("checkout.plastoids.local");
        var intentId = booking.GetProperty("paymentIntentId").GetGuid();
        learner.DefaultRequestHeaders.Remove("Idempotency-Key");

        var intent = await learner.GetFromJsonAsync<JsonElement>($"/api/v1/payments/intents/{intentId}", Json);
        (await ConfirmByWebhookAsync(intent.GetProperty("gatewayReference").GetString()!, 800000)).GetProperty("outcome").GetString().Should().Be("confirmed");

        // Confirmation arrives through payments.payment-succeeded.v1; the live session through booking.booking-confirmed.v1.
        await WaitUntilAsync(async () => (await learner.GetFromJsonAsync<JsonElement>($"/api/v1/bookings/{bookingId}", Json)).GetProperty("status").GetString() == "confirmed", TimeSpan.FromSeconds(60), "booking confirmation");
        string roomId = string.Empty;
        Guid liveSessionId = Guid.Empty;
        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var lms = scope.ServiceProvider.GetRequiredService<LmsDbContext>();
            await WaitUntilAsync(async () => await lms.LiveSessions.AsNoTracking().AnyAsync(s => s.BookingId == bookingId), TimeSpan.FromSeconds(60), "live session creation");
            var session = await lms.LiveSessions.AsNoTracking().SingleAsync(s => s.BookingId == bookingId);
            roomId = session.RoomId;
            liveSessionId = session.Id;
            session.HostUserId.Should().Be(tutorUserId);
            session.ParticipantUserIds.Should().Contain(learnerSignIn.UserId);
        }

        // Double-booking guard: the slot is taken even after the checkout lock would have expired.
        var again = await rival.GetAsync($"/api/v1/bookings/quote?tutorId={tutorId}&slotStart={slot:O}&durationMinutes=60&format=one_to_one");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadAsStringAsync()).Should().Contain("slot_taken");

        // Upcoming sessions for both parties; join token is refused until 15 minutes before the start.
        var upcoming = await learner.GetFromJsonAsync<JsonElement>("/api/v1/lms/live-sessions/upcoming", Json);
        upcoming.EnumerateArray().Select(s => s.GetProperty("liveSessionId").GetGuid()).Should().Contain(liveSessionId);
        var tooEarly = await learner.PostAsync($"/api/v1/lms/live-sessions/{liveSessionId}/join-token", null);
        tooEarly.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await tooEarly.Content.ReadAsStringAsync()).Should().Contain("join_too_early");
        (await rival.PostAsync($"/api/v1/lms/live-sessions/{liveSessionId}/join-token", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Attendance webhook from the provider → attendance event + lms.attendance-recorded.v1.
        var video = factory.Services.GetRequiredService<FakeLiveVideoProvider>();
        var joinedAt = slot.AddMinutes(2);
        var (payload, signature) = video.BuildWebhook(
        [
            new AttendanceSignal("ev_" + Guid.NewGuid().ToString("N")[..8], roomId, learnerSignIn.UserId, "joined", joinedAt),
            new AttendanceSignal("ev_" + Guid.NewGuid().ToString("N")[..8], roomId, learnerSignIn.UserId, "left", joinedAt.AddMinutes(55)),
            new AttendanceSignal("ev_" + Guid.NewGuid().ToString("N")[..8], roomId, Guid.NewGuid(), "joined", joinedAt),
        ]);
        var unsigned = await factory.CreateClient().PostAsync("/api/v1/lms/webhooks/live/fake", new StringContent(payload, Encoding.UTF8, "application/json"));
        unsigned.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var signed = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lms/webhooks/live/fake") { Content = new StringContent(payload, Encoding.UTF8, "application/json"), Headers = { { "X-Live-Signature", signature } } };
        var attendance = await factory.CreateClient().SendAsync(signed);
        attendance.StatusCode.Should().Be(HttpStatusCode.OK, await attendance.Content.ReadAsStringAsync());
        var summary = await attendance.Content.ReadFromJsonAsync<JsonElement>(Json);
        summary.GetProperty("joins").GetInt32().Should().Be(1);
        summary.GetProperty("leaves").GetInt32().Should().Be(1);
        summary.GetProperty("ignored").GetInt32().Should().Be(1, "unknown participant");

        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var lms = scope.ServiceProvider.GetRequiredService<LmsDbContext>();
            var span = await lms.Attendance.AsNoTracking().SingleAsync(a => a.LiveSessionId == liveSessionId);
            span.DurationSeconds.Should().Be(55 * 60);
            (await lms.Outbox.AsNoTracking().CountAsync(m => m.Topic == "plastoids.lms.attendance-recorded.v1" && m.PartitionKey == liveSessionId.ToString("D"))).Should().Be(1);
        }

        // Reminders (T-24h and T-1h) and the completion + hold release, driven with explicit clocks.
        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var reminders = scope.ServiceProvider.GetRequiredService<ReminderDispatcher>();
            (await reminders.RunAsync(slot.AddHours(-23), CancellationToken.None)).Should().BeGreaterThanOrEqualTo(1);
            (await reminders.RunAsync(slot.AddHours(-23), CancellationToken.None)).Should().Be(0, "each reminder goes once");
        }

        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var lifecycle = scope.ServiceProvider.GetRequiredService<BookingLifecycleJob>();
            var (completed, released) = await lifecycle.RunAsync(slot.AddHours(1).AddHours(73), CancellationToken.None);
            completed.Should().BeGreaterThanOrEqualTo(1);
            released.Should().BeGreaterThanOrEqualTo(1);
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            (await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId)).Status.Should().Be(Plastoids.Modules.Booking.Domain.BookingStatus.Completed);
            (await db.Outbox.AsNoTracking().CountAsync(m => m.Topic == "plastoids.booking.earning-released.v1")).Should().BeGreaterThanOrEqualTo(1);
            (await db.Outbox.AsNoTracking().CountAsync(m => m.Topic == "plastoids.notifications.send-requested.v1")).Should().BeGreaterThanOrEqualTo(4, "confirmation + reminder to both parties");
        }

        tutor.Dispose();
    }

    [Fact]
    public async Task Cancellation_rules_reverse_the_ledger_into_learner_credit()
    {
        var (tutor, tutorUserId, tutorId) = await AvailableTutorAsync(factory);
        var (learner, learnerSignIn) = await factory.SignInWithPhoneAsync(NewPhone(), "Cancelling learner");
        var earlySlot = NextSlot(72);
        var lateSlot = NextSlot(6);

        async Task<Guid> BookAndPayAsync(DateTime slot)
        {
            learner.DefaultRequestHeaders.Remove("Idempotency-Key");
            learner.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            var created = await learner.PostAsJsonAsync("/api/v1/bookings", new { tutorId, slotStart = slot, durationMinutes = 60, format = "one_to_one" });
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var body = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
            var id = body.GetProperty("bookingId").GetGuid();
            var intent = await learner.GetFromJsonAsync<JsonElement>($"/api/v1/payments/intents/{body.GetProperty("paymentIntentId").GetGuid()}", Json);
            await ConfirmByWebhookAsync(intent.GetProperty("gatewayReference").GetString()!, 800000);
            await WaitUntilAsync(async () => (await learner.GetFromJsonAsync<JsonElement>($"/api/v1/bookings/{id}", Json)).GetProperty("status").GetString() == "confirmed", TimeSpan.FromSeconds(60), "confirmation");
            return id;
        }

        var early = await BookAndPayAsync(earlySlot);
        var late = await BookAndPayAsync(lateSlot);

        // >24 h: 100 % to credit (2030 learner sub-ledger); tutor payable reversed.
        learner.DefaultRequestHeaders.Remove("Idempotency-Key");
        learner.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var cancelEarly = await learner.PostAsJsonAsync($"/api/v1/bookings/{early}/cancel", new { reason = "Exam moved" });
        cancelEarly.StatusCode.Should().Be(HttpStatusCode.OK, await cancelEarly.Content.ReadAsStringAsync());
        (await cancelEarly.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("refundPercent").GetInt32().Should().Be(100);

        // <24 h: 50 %.
        learner.DefaultRequestHeaders.Remove("Idempotency-Key");
        learner.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var cancelLate = await learner.PostAsJsonAsync($"/api/v1/bookings/{late}/cancel", new { reason = "Too busy", refundTo = "credit" });
        cancelLate.StatusCode.Should().Be(HttpStatusCode.OK, await cancelLate.Content.ReadAsStringAsync());
        (await cancelLate.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("refundPercent").GetInt32().Should().Be(50);

        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var ledger = scope.ServiceProvider.GetRequiredService<ILedgerService>();
            (await ledger.GetBalanceAsync(AccountCodes.LearnerRefundCredit, learnerSignIn.UserId, CancellationToken.None)).Should().Be(Money.NairaAmount(12_000), "8,000 + 4,000 of credit");
            // Tutor payable: two sessions × 85 % = 13,600 credited, then 6,800 + 3,400 reversed → 3,400 left.
            (await ledger.GetBalanceAsync(AccountCodes.TutorPayable, tutorUserId, CancellationToken.None)).Should().Be(Money.NairaAmount(3_400));
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            (await db.Outbox.AsNoTracking().CountAsync(m => m.Topic == "plastoids.booking.booking-cancelled.v1")).Should().BeGreaterThanOrEqualTo(2);
            (await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == early)).SlotKey.Should().BeNull();
        }

        // The freed slot can be booked again.
        var requote = await learner.GetAsync($"/api/v1/bookings/quote?tutorId={tutorId}&slotStart={earlySlot:O}&durationMinutes=60&format=one_to_one");
        requote.StatusCode.Should().Be(HttpStatusCode.OK, await requote.Content.ReadAsStringAsync());
        tutor.Dispose();
    }

    [Fact]
    public async Task Session_package_pays_once_and_books_without_checkout()
    {
        var (tutor, _, tutorId) = await AvailableTutorAsync(factory);
        var (learner, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Package learner");
        learner.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var created = await learner.PostAsJsonAsync("/api/v1/bookings/packages", new { tutorId, sessions = 5, durationMinutes = 60, format = "one_to_one" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var package = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        package.GetProperty("total").GetProperty("amount").GetInt64().Should().Be(4_000_000);
        var packageId = package.GetProperty("packageId").GetGuid();
        var intent = await learner.GetFromJsonAsync<JsonElement>($"/api/v1/payments/intents/{package.GetProperty("paymentIntentId").GetGuid()}", Json);
        await ConfirmByWebhookAsync(intent.GetProperty("gatewayReference").GetString()!, 4_000_000);

        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await WaitUntilAsync(async () => (await db.Packages.AsNoTracking().SingleAsync(p => p.Id == packageId)).Status == Plastoids.Modules.Booking.Domain.PackageStatus.Active, TimeSpan.FromSeconds(60), "package activation");
        }

        learner.DefaultRequestHeaders.Remove("Idempotency-Key");
        learner.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var booked = await learner.PostAsJsonAsync("/api/v1/bookings", new { tutorId, slotStart = NextSlot(30), durationMinutes = 60, format = "one_to_one", packageId });
        booked.StatusCode.Should().Be(HttpStatusCode.Created, await booked.Content.ReadAsStringAsync());
        var body = await booked.Content.ReadFromJsonAsync<JsonElement>(Json);
        body.GetProperty("status").GetString().Should().Be("confirmed");
        body.GetProperty("checkoutUrl").ValueKind.Should().Be(JsonValueKind.Null);
        var mine = await learner.GetFromJsonAsync<JsonElement>("/api/v1/bookings/me?upcoming=true", Json);
        mine.GetProperty("items").EnumerateArray().Select(b => b.GetProperty("bookingId").GetGuid()).Should().Contain(body.GetProperty("bookingId").GetGuid());
        tutor.Dispose();
    }
}
