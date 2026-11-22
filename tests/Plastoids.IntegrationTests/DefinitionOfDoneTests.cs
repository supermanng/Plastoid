using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Ledger.Jobs;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.Modules.Payouts.Jobs;
using Plastoids.Modules.Sponsorship.Jobs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

/// <summary>
/// Build prompt §2.12 definition-of-done scenario: sponsor verified → campaign funded → applications → selection →
/// enrolment → live attendance → assessment → certificate → tutor payout → sponsor funnel report, with the ledger
/// reconciling to zero breaks. Two seats stand in for the blueprint's 150.
/// </summary>
[Collection(ApiCollection.Name)]
public class DefinitionOfDoneTests(PlastoidsApiFactory factory)
{
    private static void Key(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
    }

    private async Task<Money> BalanceAsync(string code, Guid? owner = null)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILedgerService>().GetBalanceAsync(code, owner, CancellationToken.None);
    }

    [Fact]
    public async Task MVP_definition_of_done_runs_end_to_end_with_zero_reconciliation_breaks()
    {
        var now = DateTime.UtcNow;
        var staff = await factory.SignInAsStaffAsync();

        // 1. Tutor publishes a course with a quiz and opens a cohort offering + cohort.
        var (tutor, tutorUserId, courseId, slug, docLessonId, quizLessonId) = await LearningAndCertificatesTests.CourseWithQuizAsync(factory);
        var offering = await tutor.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/offerings", new { format = "cohort", price = new { amount = 10_000_000, currency = "NGN" }, fundingTypes = new[] { "sponsored" }, capacity = 30, startAt = now.AddDays(10), durationWeeks = 8 });
        offering.StatusCode.Should().Be(HttpStatusCode.Created, await offering.Content.ReadAsStringAsync());
        var offeringId = (await offering.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("offeringId").GetGuid();
        var cohort = await tutor.PostAsJsonAsync("/api/v1/lms/cohorts", new { courseId, offeringId, name = "DoD cohort", startsAt = now.AddDays(10), endsAt = now.AddDays(66), capacity = 25, programmeId = (Guid?)null });
        cohort.StatusCode.Should().Be(HttpStatusCode.Created, await cohort.Content.ReadAsStringAsync());
        var cohortId = (await cohort.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("cohortId").GetGuid();

        // 2. Sponsor verified → campaign → quote → invoice paid → review → published (2 seats, fully sponsored).
        var (owner, organisationId, _) = await SponsorshipAndApplicationsTests.VerifiedSponsorAsync(factory, "DoD Foundation " + Guid.NewGuid().ToString("N")[..6]);
        var programmes = $"/api/v1/sponsorship/organisations/{organisationId}/programmes";
        var created = await owner.PostAsJsonAsync(programmes, new { name = "Data Analytics for Graduates (DoD)", objectives = "Definition of done" });
        var programmeId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("programmeId").GetGuid();
        var startsAt = now.AddDays(3);
        var step = await owner.PutAsJsonAsync($"{programmes}/{programmeId}", new
        {
            courseId,
            offeringId,
            seats = 2,
            sponsorSharePercent = 100,
            eligibilityRules = new { minAge = 18 },
            entryAssessment = new { passMarkPercent = 50, questions = new object[] { new { prompt = "2 + 2", options = new[] { "3", "4" }, correctIndex = 1 } } },
            scoringRubric = new { assessmentWeight = 50, criteria = new object[] { new { key = "motivation", weight = 1, max = 5 } } },
            applicationsCloseAt = now.AddDays(2),
            startsAt,
            endsAt = startsAt.AddDays(91),
            selectionMode = "sponsor",
        });
        step.StatusCode.Should().Be(HttpStatusCode.OK, await step.Content.ReadAsStringAsync());
        var quote = await (await owner.PostAsync($"{programmes}/{programmeId}/quote", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var total = quote.GetProperty("total").GetProperty("amount").GetInt64();
        total.Should().Be(20_000_000 + 300_000_000 + 2_400_000 + 22_680_000);
        Key(owner);
        var invoice = await (await owner.PostAsJsonAsync($"{programmes}/{programmeId}/quote/approve", new { dueDays = 14 })).Content.ReadFromJsonAsync<JsonElement>(Json);
        Key(staff);
        (await staff.PostAsJsonAsync($"/api/v1/admin/sponsorship/programmes/{programmeId}/review", new { decision = "approved", rulesLawful = true, notes = "ok" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var gateway = factory.Services.GetRequiredService<FakePaymentGateway>();
        var (payload, signature) = gateway.BuildWebhook("virtual_account.credit", "va_" + Guid.NewGuid().ToString("N")[..10], Money.Kobo(total), DateTime.UtcNow, invoice.GetProperty("virtualAccountNumber").GetString());
        var paid = await factory.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/fake") { Content = new StringContent(payload, Encoding.UTF8, "application/json"), Headers = { { "x-paystack-signature", signature } } });
        paid.StatusCode.Should().Be(HttpStatusCode.OK, await paid.Content.ReadAsStringAsync());
        await WaitUntilAsync(async () => (await owner.GetFromJsonAsync<JsonElement>($"{programmes}/{programmeId}", Json)).GetProperty("isFunded").GetBoolean(), TimeSpan.FromSeconds(60), "programme funding");
        var published = await owner.PostAsync($"{programmes}/{programmeId}/publish", null);
        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());

        // 3. Two learners apply, sit the entry assessment, are scored, accepted, shortlisted and offered seats.
        var learners = new List<(HttpClient Client, Guid UserId, Guid ApplicationId)>();
        for (var i = 1; i <= 2; i++)
        {
            var (client, signIn) = await factory.SignInWithPhoneAsync(NewPhone(), $"DoD learner {i}");
            var applied = await client.PostAsJsonAsync($"/api/v1/applications/programmes/{programmeId}/applications", new { answers = new { fullName = $"DoD learner {i}", countryOfResidence = "NG", motivation = "Data" }, submit = true });
            applied.StatusCode.Should().Be(HttpStatusCode.Created, await applied.Content.ReadAsStringAsync());
            var applicationId = (await applied.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("applicationId").GetGuid();
            (await client.PostAsync($"/api/v1/applications/{applicationId}/assessment", null)).StatusCode.Should().Be(HttpStatusCode.Created);
            (await client.PutAsJsonAsync($"/api/v1/applications/{applicationId}/assessment", new { answers = new Dictionary<string, object> { ["0"] = 1 } })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await staff.PostAsJsonAsync($"/api/v1/admin/applications/{applicationId}/score", new { scores = new Dictionary<string, decimal> { ["motivation"] = 5 } })).StatusCode.Should().Be(HttpStatusCode.OK);
            learners.Add((client, signIn.UserId, applicationId));
        }

        Key(staff);
        (await staff.PostAsJsonAsync($"/api/v1/admin/applications/programmes/{programmeId}/decisions", new { decisions = learners.Select(l => new { applicationId = l.ApplicationId, decision = "accepted", reason = "Qualified" }).ToArray() })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.PostAsync($"/api/v1/applications/programmes/{programmeId}/shortlist/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        Key(staff);
        var offers = await (await staff.PostAsync($"/api/v1/admin/applications/programmes/{programmeId}/offers", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        offers.GetProperty("sent").GetInt32().Should().Be(2);

        // 4. Offers accepted → sponsored enrolments in the cohort → seats filled (entry 7).
        var enrolmentIds = new List<Guid>();
        foreach (var (client, _, applicationId) in learners)
        {
            Key(client);
            var accepted = await client.PostAsJsonAsync($"/api/v1/applications/{applicationId}/offer/accept", new { });
            accepted.StatusCode.Should().Be(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());
            (await accepted.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetString().Should().Be("enrolled");
            JsonElement enrolments = default;
            await WaitUntilAsync(async () =>
            {
                enrolments = await client.GetFromJsonAsync<JsonElement>("/api/v1/lms/enrolments/me", Json);
                return enrolments.EnumerateArray().Any(e => e.GetProperty("courseSlug").GetString() == slug);
            }, TimeSpan.FromSeconds(60), "sponsored enrolment");
            enrolmentIds.Add(enrolments.EnumerateArray().Single(e => e.GetProperty("courseSlug").GetString() == slug).GetProperty("enrolmentId").GetGuid());
        }

        await WaitUntilAsync(async () => (await owner.GetFromJsonAsync<JsonElement>($"{programmes}/{programmeId}/funnel", Json)).GetProperty("seatsFilled").GetInt32() == 2, TimeSpan.FromSeconds(60), "seats filled");
        (await BalanceAsync(AccountCodes.PlatformSeatFee)).AmountKobo.Should().BeGreaterThanOrEqualTo(2_400_000);

        // 5. Live attendance: the tutor schedules cohort classes; the provider reports both learners joined.
        var scheduled = await tutor.PostAsJsonAsync($"/api/v1/lms/cohorts/{cohortId}/sessions", new { weekdays = new[] { "monday" }, startTimeUtc = "18:00", durationMinutes = 90, titlePrefix = "DoD class" });
        scheduled.StatusCode.Should().Be(HttpStatusCode.Created, await scheduled.Content.ReadAsStringAsync());
        string roomId;
        DateTime sessionStart;
        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var lms = scope.ServiceProvider.GetRequiredService<LmsDbContext>();
            var session = await lms.LiveSessions.AsNoTracking().Where(s => s.CohortId == cohortId).OrderBy(s => s.StartsAt).FirstAsync();
            roomId = session.RoomId;
            sessionStart = session.StartsAt;
        }

        var video = factory.Services.GetRequiredService<FakeLiveVideoProvider>();
        var signals = learners.SelectMany(l => new[]
        {
            new AttendanceSignal("ev_" + Guid.NewGuid().ToString("N")[..8], roomId, l.UserId, "joined", sessionStart.AddMinutes(2)),
            new AttendanceSignal("ev_" + Guid.NewGuid().ToString("N")[..8], roomId, l.UserId, "left", sessionStart.AddMinutes(80)),
        }).ToList();
        var (attendancePayload, attendanceSignature) = video.BuildWebhook(signals);
        var attendance = await factory.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/lms/webhooks/live/fake") { Content = new StringContent(attendancePayload, Encoding.UTF8, "application/json"), Headers = { { "X-Live-Signature", attendanceSignature } } });
        attendance.StatusCode.Should().Be(HttpStatusCode.OK, await attendance.Content.ReadAsStringAsync());

        // 6. Assessment and completion → certificates issued and verifiable.
        for (var i = 0; i < 2; i++)
        {
            var (client, _, _) = learners[i];
            var enrolmentId = enrolmentIds[i];
            (await client.PostAsJsonAsync($"/api/v1/lms/enrolments/{enrolmentId}/progress", new { items = new object[] { new { lessonId = docLessonId, status = "completed", positionSeconds = 0, page = 3, percentComplete = 100, clientUpdatedAt = DateTime.UtcNow } } })).StatusCode.Should().Be(HttpStatusCode.OK);
            var attempt = await (await client.PostAsync($"/api/v1/lms/enrolments/{enrolmentId}/lessons/{quizLessonId}/attempts", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
            var submitted = await client.PostAsJsonAsync($"/api/v1/lms/attempts/{attempt.GetProperty("attemptId").GetGuid()}/submit", new { answers = new Dictionary<string, object> { ["0"] = 1, ["1"] = 0 } });
            submitted.StatusCode.Should().Be(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
            (await submitted.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("enrolmentCompleted").GetBoolean().Should().BeTrue();
            JsonElement certificates = default;
            await WaitUntilAsync(async () =>
            {
                certificates = await client.GetFromJsonAsync<JsonElement>("/api/v1/certificates/me", Json);
                return certificates.EnumerateArray().Any(c => c.GetProperty("enrolmentId").GetGuid() == enrolmentId);
            }, TimeSpan.FromSeconds(60), "certificate");
            var code = certificates.EnumerateArray().Single(c => c.GetProperty("enrolmentId").GetGuid() == enrolmentId).GetProperty("publicCode").GetString();
            (await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/verify/{code}", Json)).GetProperty("status").GetString().Should().Be("valid");
        }

        // 7. Programme months → tutor earnings (entries 8, 9) → weekly payout with maker–checker → settled (entry 4).
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<MonthlyFeesJob>().RunAsync(startsAt.AddDays(95), CancellationToken.None)).Should().Be(3);
        }

        var expectedTutorEarnings = 3 * (10_000_000L * 2 / 3);
        (await BalanceAsync(AccountCodes.TutorPayable, tutorUserId)).AmountKobo.Should().Be(expectedTutorEarnings);
        await WaitUntilAsync(async () => (await tutor.GetFromJsonAsync<JsonElement>("/api/v1/payouts/me", Json)).GetProperty("available").GetProperty("amount").GetInt64() == expectedTutorEarnings, TimeSpan.FromSeconds(60), "programme earnings payable");
        var resolver = factory.Services.GetRequiredService<FakeBankAccountResolver>();
        var accountNumber = "02" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        resolver.KnownAccounts[accountNumber] = "COURSE AUTHOR";
        (await tutor.PutAsJsonAsync("/api/v1/tutors/me/payout-account", new { bankCode = "058", accountNumber })).StatusCode.Should().Be(HttpStatusCode.OK);
        Guid batchId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            batchId = (await scope.ServiceProvider.GetRequiredService<WeeklyPayoutRunJob>().RunAsync(DateTime.UtcNow, CancellationToken.None)) ?? Guid.Empty;
        }

        if (batchId == Guid.Empty)
        {
            Key(staff);
            var diagnostic = await staff.PostAsync("/api/v1/admin/payouts/batches", null);
            throw new Xunit.Sdk.XunitException("The weekly run drafted no batch; manual draft said: " + await diagnostic.Content.ReadAsStringAsync());
        }

        Key(staff);
        (await staff.PostAsync($"/api/v1/admin/payouts/batches/{batchId}/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        Key(staff);
        (await staff.PostAsync($"/api/v1/admin/payouts/batches/{batchId}/submit", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var batch = await staff.GetFromJsonAsync<JsonElement>($"/api/v1/admin/payouts/batches/{batchId}", Json);
        var payout = batch.GetProperty("payouts").EnumerateArray().Single(p => p.TryGetProperty("tutorUserId", out var tu) && tu.ValueKind == JsonValueKind.String && tu.GetGuid() == tutorUserId);
        var provider = factory.Services.GetRequiredService<FakePayoutProvider>();
        var (settlePayload, settleSignature) = provider.BuildWebhook("transfer.success", payout.GetProperty("providerReference").GetString()!, Money.Kobo(payout.GetProperty("amount").GetProperty("amount").GetInt64()), DateTime.UtcNow, "NIP-DOD");
        var settled = await factory.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/payouts/webhooks/fake") { Content = new StringContent(settlePayload, Encoding.UTF8, "application/json"), Headers = { { "x-payout-signature", settleSignature } } });
        settled.StatusCode.Should().Be(HttpStatusCode.OK, await settled.Content.ReadAsStringAsync());
        (await BalanceAsync(AccountCodes.TutorPayable, tutorUserId)).Should().Be(Money.Zero(), "the tutor was paid correctly");

        // 8. Sponsor funnel report and impact report.
        Dictionary<string, int> stages = [];
        JsonElement funnel = default;
        await WaitUntilAsync(async () =>
        {
            funnel = await owner.GetFromJsonAsync<JsonElement>($"{programmes}/{programmeId}/funnel", Json);
            stages = funnel.GetProperty("stages").EnumerateArray().ToDictionary(s => s.GetProperty("stage").GetString()!, s => s.GetProperty("count").GetInt32());
            return stages["certified"] == 2 && funnel.GetProperty("sessionsAttended").GetInt32() >= 2;
        }, TimeSpan.FromSeconds(60), "funnel certified + attendance");
        stages.Should().BeEquivalentTo(new Dictionary<string, int> { ["applied"] = 2, ["offered"] = 2, ["enrolled"] = 2, ["active"] = 2, ["completed"] = 2, ["certified"] = 2 });
        (await owner.GetStringAsync($"{programmes}/{programmeId}/funnel?format=csv")).Should().Contain("certified,2,100");
        var report = await owner.GetAsync($"{programmes}/{programmeId}/impact-report");
        report.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        // 9. The ledger reconciles: every entry nets to zero and assets cover liabilities (zero breaks).
        await using (var connection = new MySqlConnection(Plastoids.Infrastructure.InfrastructureServiceCollectionExtensions.EnsureGuidFormat(factory.MySqlConnectionString)))
        {
            await connection.OpenAsync();
            await using var unbalanced = connection.CreateCommand();
            unbalanced.CommandText = "SELECT COUNT(*) FROM (SELECT journal_entry_id, SUM(debit_kobo) - SUM(credit_kobo) AS net FROM ledger_postings GROUP BY journal_entry_id HAVING net <> 0) AS x";
            Convert.ToInt64(await unbalanced.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture).Should().Be(0);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var (assets, liabilities) = await scope.ServiceProvider.GetRequiredService<ReconciliationJob>().RunAsync(CancellationToken.None);
            assets.Should().BeGreaterThanOrEqualTo(liabilities);
        }

        (await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/payouts/queue", Json)).GetProperty("payoutsFrozen").GetBoolean().Should().BeFalse();
        tutor.Dispose();
    }
}
