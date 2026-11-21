using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Sponsorship.Jobs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SponsorshipAndApplicationsTests(PlastoidsApiFactory factory)
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

    private static HttpRequestMessage SignedWebhook(string payload, string signature) =>
        new(HttpMethod.Post, "/api/v1/payments/webhooks/fake") { Content = new StringContent(payload, Encoding.UTF8, "application/json"), Headers = { { "x-paystack-signature", signature } } };

    /// <summary>A verified sponsor organisation (blueprint §10 week −8) with its owner signed in.</summary>
    internal static async Task<(HttpClient Owner, Guid OrganisationId, Guid OwnerUserId)> VerifiedSponsorAsync(PlastoidsApiFactory factory, string name)
    {
        var (owner, signIn) = await factory.SignInWithPhoneAsync(NewPhone(), "Sponsor owner", "1980-03-03");
        var org = await owner.PostAsJsonAsync("/api/v1/identity/organisations", new { name, type = "sponsor", cacNumber = "RC" + Random.Shared.Next(100000, 999999), signatoryName = "Mrs Adebayo", signatoryEmail = "csr@sponsor.example" });
        org.StatusCode.Should().Be(HttpStatusCode.Created, await org.Content.ReadAsStringAsync());
        var organisationId = (await org.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("organisationId").GetGuid();
        var staff = await factory.SignInAsStaffAsync();
        Key(staff);
        var verified = await staff.PostAsJsonAsync($"/api/v1/admin/identity/organisations/{organisationId}/verification", new { decision = "verified" });
        verified.IsSuccessStatusCode.Should().BeTrue(await verified.Content.ReadAsStringAsync());
        return (owner, organisationId, signIn.UserId);
    }

    [Fact]
    public async Task Sponsored_programme_runs_from_campaign_to_closeout()
    {
        var now = DateTime.UtcNow;
        var (tutor, tutorUserId, slug, courseId) = await CatalogueTests.PublishedCourseAsync(factory);
        var course = await tutor.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/{slug}", Json);
        var offeringId = course.GetProperty("offerings")[0].GetProperty("offeringId").GetGuid();
        var (owner, organisationId, _) = await VerifiedSponsorAsync(factory, "Access CSR " + Guid.NewGuid().ToString("N")[..6]);
        var programmes = $"/api/v1/sponsorship/organisations/{organisationId}/programmes";

        // Stepper: three saves, the last one invalid (protected characteristic).
        var step1 = await owner.PostAsJsonAsync(programmes, new { name = "Data Analytics for Graduates", objectives = "Employability for 3 graduates" });
        step1.StatusCode.Should().Be(HttpStatusCode.Created, await step1.Content.ReadAsStringAsync());
        var programmeId = (await step1.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("programmeId").GetGuid();
        var step2 = await owner.PutAsJsonAsync($"{programmes}/{programmeId}", new { courseId, offeringId, seats = 3, sponsorSharePercent = 70, budget = new { amount = 500_000_000, currency = "NGN" } });
        step2.StatusCode.Should().Be(HttpStatusCode.OK, await step2.Content.ReadAsStringAsync());
        var startsAt = now.AddDays(3);
        var step3 = await owner.PutAsJsonAsync($"{programmes}/{programmeId}", new
        {
            eligibilityRules = new { minAge = 21, residency = new[] { "NG" }, notPreviouslySponsored = true },
            entryAssessment = new { passMarkPercent = 50, timeLimitMinutes = 15, questions = new object[] { new { prompt = "2 + 2", options = new[] { "3", "4" }, correctIndex = 1 }, new { prompt = "Capital of Nigeria", answer = "Abuja" } } },
            scoringRubric = new { assessmentWeight = 50, criteria = new object[] { new { key = "motivation", weight = 1, max = 5 } } },
            applicationsCloseAt = now.AddDays(2),
            startsAt,
            endsAt = startsAt.AddDays(91),
            selectionMode = "sponsor",
        });
        step3.StatusCode.Should().Be(HttpStatusCode.OK, await step3.Content.ReadAsStringAsync());
        var discriminatory = await owner.PutAsJsonAsync($"{programmes}/{programmeId}", new { eligibilityRules = new { gender = new[] { "female" } } });
        discriminatory.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var view = await owner.GetFromJsonAsync<JsonElement>($"{programmes}/{programmeId}", Json);
        view.GetProperty("tuitionPerSeat").GetProperty("amount").GetInt64().Should().Be(4_500_000, "tuition defaults to the offering price");
        view.GetProperty("coPayPerSeat").GetProperty("amount").GetInt64().Should().Be(1_350_000);

        // Quote (story F1): tuition 3 × 31,500; management fee floor ₦3m; seat fee 3 × 12,000; VAT 7.5 % on fees.
        var quote = await owner.PostAsync($"{programmes}/{programmeId}/quote", null);
        quote.StatusCode.Should().Be(HttpStatusCode.OK, await quote.Content.ReadAsStringAsync());
        var q = await quote.Content.ReadFromJsonAsync<JsonElement>(Json);
        var lines = q.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("kind").GetString()!, l => l.GetProperty("amount").GetProperty("amount").GetInt64());
        lines["tuition"].Should().Be(9_450_000);
        lines["management_fee"].Should().Be(300_000_000);
        lines["seat_fee"].Should().Be(3_600_000);
        lines["vat"].Should().Be(22_770_000);
        q.GetProperty("total").GetProperty("amount").GetInt64().Should().Be(335_820_000);

        Key(owner);
        var approved = await owner.PostAsJsonAsync($"{programmes}/{programmeId}/quote/approve", new { dueDays = 14 });
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        var invoice = await approved.Content.ReadFromJsonAsync<JsonElement>(Json);
        invoice.GetProperty("total").GetProperty("amount").GetInt64().Should().Be(335_820_000);
        var virtualAccount = invoice.GetProperty("virtualAccountNumber").GetString()!;

        (await owner.PostAsync($"{programmes}/{programmeId}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "not reviewed yet");
        var staff = await factory.SignInAsStaffAsync();
        Key(staff);
        var review = await staff.PostAsJsonAsync($"/api/v1/admin/sponsorship/programmes/{programmeId}/review", new { decision = "approved", rulesLawful = true, notes = "Rules are lawful and non-discriminatory." });
        review.StatusCode.Should().Be(HttpStatusCode.OK, await review.Content.ReadAsStringAsync());
        (await owner.PostAsync($"{programmes}/{programmeId}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "not funded yet");

        // Sponsor pays the invoice (entry 6) → payments.invoice-paid.v1 → programme funded.
        var gateway = factory.Services.GetRequiredService<FakePaymentGateway>();
        var (payload, signature) = gateway.BuildWebhook("virtual_account.credit", "va_" + Guid.NewGuid().ToString("N")[..10], Money.Kobo(335_820_000), DateTime.UtcNow, virtualAccount);
        (await (await factory.CreateClient().SendAsync(SignedWebhook(payload, signature))).Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("outcome").GetString().Should().Be("matched");
        await WaitUntilAsync(async () => (await owner.GetFromJsonAsync<JsonElement>($"{programmes}/{programmeId}", Json)).GetProperty("isFunded").GetBoolean(), TimeSpan.FromSeconds(60), "programme funding from invoice-paid");
        (await BalanceAsync(AccountCodes.ProgrammeFundsHeld, programmeId)).Should().Be(Money.Kobo(313_050_000));

        var published = await owner.PostAsync($"{programmes}/{programmeId}/publish", null);
        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
        (await published.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("seatsUnlocked").GetInt32().Should().Be(3);
        var opportunities = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/sponsorship/opportunities?limit=200", Json);
        opportunities.GetProperty("items").EnumerateArray().Should().Contain(o => o.GetProperty("programmeId").GetGuid() == programmeId);

        // Learners apply (story F3). Five eligible, one duplicate NIN, one ineligible.
        var learners = new List<(HttpClient Client, Guid UserId, Guid ApplicationId)>();
        for (var i = 1; i <= 5; i++)
        {
            var (client, signIn) = await factory.SignInWithPhoneAsync(NewPhone(), $"Applicant {i}");
            var applied = await client.PostAsJsonAsync($"/api/v1/applications/programmes/{programmeId}/applications", new { answers = new { fullName = $"Applicant {i}", nin = $"1234567890{i}", countryOfResidence = "NG", educationLevel = "graduate", motivation = "I want to work with data." }, submit = true });
            applied.StatusCode.Should().Be(HttpStatusCode.Created, await applied.Content.ReadAsStringAsync());
            var body = await applied.Content.ReadFromJsonAsync<JsonElement>(Json);
            body.GetProperty("status").GetString().Should().Be("submitted");
            learners.Add((client, signIn.UserId, body.GetProperty("applicationId").GetGuid()));
        }

        var (duplicate, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Duplicate");
        var dup = await duplicate.PostAsJsonAsync($"/api/v1/applications/programmes/{programmeId}/applications", new { answers = new { fullName = "Someone Else", nin = "12345678901", countryOfResidence = "NG" }, submit = true });
        var dupBody = await dup.Content.ReadFromJsonAsync<JsonElement>(Json);
        dupBody.GetProperty("status").GetString().Should().Be("merged");
        dupBody.GetProperty("flagReason").GetString().Should().Be("duplicate_identity");

        var (foreign, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Abroad");
        var check = await foreign.PostAsJsonAsync($"/api/v1/sponsorship/opportunities/{programmeId}/eligibility", new { countryOfResidence = "GH", educationLevel = "graduate" });
        check.StatusCode.Should().Be(HttpStatusCode.OK, await check.Content.ReadAsStringAsync());
        var eligibility = await check.Content.ReadFromJsonAsync<JsonElement>(Json);
        eligibility.GetProperty("eligible").GetBoolean().Should().BeFalse();
        eligibility.GetProperty("reasons").EnumerateArray().Select(r => r.GetString()).Should().Contain("residency_not_eligible");
        eligibility.GetProperty("evaluationMs").GetInt32().Should().BeLessThan(1000);
        (await foreign.PostAsJsonAsync($"/api/v1/applications/programmes/{programmeId}/applications", new { answers = new { countryOfResidence = "GH" }, submit = true })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Draft save-and-resume for applicant 1 is refused after submission; drafts work for a fresh learner.
        var (drafter, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Drafter");
        var draft = await drafter.PostAsJsonAsync($"/api/v1/applications/programmes/{programmeId}/applications", new { answers = new { motivation = "half done" }, submit = false });
        draft.StatusCode.Should().Be(HttpStatusCode.OK, await draft.Content.ReadAsStringAsync());
        (await draft.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetString().Should().Be("draft");

        // Entry assessment (timed, Lms engine): applicants 1–3 pass, applicant 4 fails, applicant 5 skips it.
        for (var i = 0; i < 4; i++)
        {
            var (client, _, applicationId) = learners[i];
            var started = await client.PostAsync($"/api/v1/applications/{applicationId}/assessment", null);
            started.StatusCode.Should().Be(HttpStatusCode.Created, await started.Content.ReadAsStringAsync());
            var assessment = await started.Content.ReadFromJsonAsync<JsonElement>(Json);
            assessment.GetProperty("questions").GetProperty("questions")[0].TryGetProperty("correctIndex", out _).Should().BeFalse();
            assessment.GetProperty("secondsRemaining").GetInt32().Should().BeInRange(800, 900);
            var answers = i < 3 ? new Dictionary<string, object> { ["0"] = 1, ["1"] = "abuja" } : new Dictionary<string, object> { ["0"] = 0, ["1"] = "Lagos" };
            var submitted = await client.PutAsJsonAsync($"/api/v1/applications/{applicationId}/assessment", new { answers });
            submitted.StatusCode.Should().Be(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
            (await submitted.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("passed").GetBoolean().Should().Be(i < 3);
            (await client.PostAsync($"/api/v1/applications/{applicationId}/assessment", null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "one attempt only");
        }

        // Blind review queue, scoring, bulk decisions (story F4).
        var blind = await staff.GetFromJsonAsync<JsonElement>($"/api/v1/admin/applications/programmes/{programmeId}/queue?blind=true", Json);
        var blindItems = blind.GetProperty("page").GetProperty("items").EnumerateArray().ToList();
        blindItems.Should().HaveCount(6, "five applicants plus the merged duplicate");
        blindItems.Should().AllSatisfy(item =>
        {
            item.GetProperty("learnerId").ValueKind.Should().Be(JsonValueKind.Null);
            item.GetProperty("answers").TryGetProperty("nin", out _).Should().BeFalse();
            item.GetProperty("answers").TryGetProperty("fullName", out _).Should().BeFalse();
        });
        var rubricScores = new[] { 5, 4, 3, 5, 2 };
        for (var i = 0; i < 5; i++)
        {
            var scored = await staff.PostAsJsonAsync($"/api/v1/admin/applications/{learners[i].ApplicationId}/score", new { scores = new Dictionary<string, decimal> { ["motivation"] = rubricScores[i] } });
            scored.StatusCode.Should().Be(HttpStatusCode.OK, await scored.Content.ReadAsStringAsync());
        }

        (await staff.PostAsJsonAsync($"/api/v1/admin/applications/{learners[0].ApplicationId}/score", new { scores = new Dictionary<string, decimal> { ["gender"] = 1 } })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Key(staff);
        var decided = await staff.PostAsJsonAsync($"/api/v1/admin/applications/programmes/{programmeId}/decisions", new
        {
            decisions = new object[]
            {
                new { applicationId = learners[0].ApplicationId, decision = "accepted", reason = "Top score" },
                new { applicationId = learners[1].ApplicationId, decision = "accepted", reason = "Strong" },
                new { applicationId = learners[2].ApplicationId, decision = "accepted", reason = "Good" },
                new { applicationId = learners[3].ApplicationId, decision = "accepted", reason = "Accepted despite failing the quiz: strong motivation" },
                new { applicationId = learners[4].ApplicationId, decision = "rejected", reason = "No assessment taken" },
                new { applicationId = dupBody.GetProperty("applicationId").GetGuid(), decision = "accepted", reason = "oops" },
            },
        });
        decided.StatusCode.Should().Be(HttpStatusCode.OK, await decided.Content.ReadAsStringAsync());
        var decisions = await decided.Content.ReadFromJsonAsync<JsonElement>(Json);
        decisions.GetProperty("applied").GetInt32().Should().Be(5);
        decisions.GetProperty("failures")[0].GetProperty("code").GetString().Should().Be("application_flagged");

        // Offers need the sponsor's shortlist approval; 3 seats → 3 offers, 1 waitlisted.
        Key(staff);
        (await staff.PostAsync($"/api/v1/admin/applications/programmes/{programmeId}/offers", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await learners[0].Client.PostAsync($"/api/v1/applications/programmes/{programmeId}/shortlist/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var shortlist = await owner.PostAsync($"/api/v1/applications/programmes/{programmeId}/shortlist/approve", null);
        shortlist.StatusCode.Should().Be(HttpStatusCode.OK, await shortlist.Content.ReadAsStringAsync());
        Key(staff);
        var offers = await staff.PostAsync($"/api/v1/admin/applications/programmes/{programmeId}/offers", null);
        offers.StatusCode.Should().Be(HttpStatusCode.OK, await offers.Content.ReadAsStringAsync());
        var offersBody = await offers.Content.ReadFromJsonAsync<JsonElement>(Json);
        offersBody.GetProperty("sent").GetInt32().Should().Be(3);
        offersBody.GetProperty("waitlisted").GetInt32().Should().Be(1);
        var fourth = (await learners[3].Client.GetFromJsonAsync<JsonElement>("/api/v1/applications/me", Json))[0];
        fourth.GetProperty("status").GetString().Should().Be("waitlisted");

        // Applicant 1 accepts: partial sponsorship → co-pay checkout → payment confirms the seat → Lms enrols, seat fee earned (entry 7).
        Key(learners[0].Client);
        var accept = await learners[0].Client.PostAsJsonAsync($"/api/v1/applications/{learners[0].ApplicationId}/offer/accept", new { });
        accept.StatusCode.Should().Be(HttpStatusCode.OK, await accept.Content.ReadAsStringAsync());
        var acceptBody = await accept.Content.ReadFromJsonAsync<JsonElement>(Json);
        acceptBody.GetProperty("status").GetString().Should().Be("pendingcopay");
        var coPay = acceptBody.GetProperty("coPay");
        coPay.GetProperty("amount").GetProperty("amount").GetInt64().Should().Be(1_350_000);
        var (chargePayload, chargeSignature) = gateway.BuildWebhook("charge.success", coPay.GetProperty("gatewayReference").GetString()!, Money.Kobo(1_350_000), DateTime.UtcNow);
        (await factory.CreateClient().SendAsync(SignedWebhook(chargePayload, chargeSignature))).StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitUntilAsync(async () => (await learners[0].Client.GetFromJsonAsync<JsonElement>("/api/v1/applications/me", Json))[0].GetProperty("status").GetString() == "enrolled", TimeSpan.FromSeconds(60), "co-pay confirmation");
        await WaitUntilAsync(async () => (await learners[0].Client.GetFromJsonAsync<JsonElement>("/api/v1/lms/enrolments/me", Json)).EnumerateArray().Any(e => e.GetProperty("courseSlug").GetString() == slug), TimeSpan.FromSeconds(60), "sponsored enrolment");
        await WaitUntilAsync(async () =>
        {
            var funnel = await owner.GetFromJsonAsync<JsonElement>($"{programmes}/{programmeId}/funnel", Json);
            return funnel.GetProperty("stages").EnumerateArray().Single(s => s.GetProperty("stage").GetString() == "active").GetProperty("count").GetInt32() == 1;
        }, TimeSpan.FromSeconds(60), "funnel: active learner");
        (await BalanceAsync(AccountCodes.ProgrammeFundsHeld, programmeId)).Should().Be(Money.Kobo(313_050_000 + 1_350_000 - 1_200_000), "co-pay joins the pool, the seat fee leaves it");

        // Applicant 2 declines → the waitlisted applicant 4 is offered the seat at once.
        var declined = await learners[1].Client.PostAsync($"/api/v1/applications/{learners[1].ApplicationId}/offer/decline", null);
        declined.StatusCode.Should().Be(HttpStatusCode.OK, await declined.Content.ReadAsStringAsync());
        (await declined.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("backfilledOffers").GetInt32().Should().Be(1);
        (await learners[3].Client.GetFromJsonAsync<JsonElement>("/api/v1/applications/me", Json))[0].GetProperty("status").GetString().Should().Be("offered");

        // Applicant 5 appeals the rejection; staff uphold it → back on the waitlist.
        var appeal = await learners[4].Client.PostAsJsonAsync($"/api/v1/applications/{learners[4].ApplicationId}/appeals", new { reason = "I could not take the assessment because of a network outage in my area." });
        appeal.StatusCode.Should().Be(HttpStatusCode.Created, await appeal.Content.ReadAsStringAsync());
        var appealId = (await appeal.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("appealId").GetGuid();
        Key(staff);
        var resolved = await staff.PostAsJsonAsync($"/api/v1/admin/applications/appeals/{appealId}/resolve", new { decision = "upheld", reason = "Verified outage" });
        resolved.StatusCode.Should().Be(HttpStatusCode.OK, await resolved.Content.ReadAsStringAsync());
        (await resolved.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("applicationStatus").GetString().Should().Be("waitlisted");

        // Decisions are append-only.
        await using (var connection = new MySqlConnection(Plastoids.Infrastructure.InfrastructureServiceCollectionExtensions.EnsureGuidFormat(factory.MySqlConnectionString)))
        {
            await connection.OpenAsync();
            await using var tamper = connection.CreateCommand();
            tamper.CommandText = "UPDATE applications_decisions SET reason = 'edited' LIMIT 1";
            var failure = await Record.ExceptionAsync(() => tamper.ExecuteNonQueryAsync());
            failure.Should().BeOfType<MySqlException>().Which.Message.Should().Contain("append-only");
        }

        // Monthly fees (entries 8 and 9) for the three programme months, computed on the one filled seat.
        var before4030 = await BalanceAsync(AccountCodes.ProgrammeManagementFee);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<MonthlyFeesJob>().RunAsync(startsAt.AddDays(95), CancellationToken.None)).Should().Be(3);
            (await scope.ServiceProvider.GetRequiredService<MonthlyFeesJob>().RunAsync(startsAt.AddDays(96), CancellationToken.None)).Should().Be(0, "idempotent");
        }

        ((await BalanceAsync(AccountCodes.ProgrammeManagementFee)) - before4030).Should().Be(Money.Kobo(99_999_999));
        (await BalanceAsync(AccountCodes.TutorPayable, tutorUserId)).Should().Be(Money.Kobo(3_150_000));
        var budget = await owner.GetFromJsonAsync<JsonElement>($"{programmes}/{programmeId}/budget", Json);
        budget.GetProperty("months").GetArrayLength().Should().Be(3);
        budget.GetProperty("seatFeesEarned").GetProperty("amount").GetInt64().Should().Be(1_200_000);

        // Funnel, CSV and the impact report.
        Dictionary<string, int> stages = [];
        await WaitUntilAsync(async () =>
        {
            var funnelView = await owner.GetFromJsonAsync<JsonElement>($"{programmes}/{programmeId}/funnel", Json);
            stages = funnelView.GetProperty("stages").EnumerateArray().ToDictionary(s => s.GetProperty("stage").GetString()!, s => s.GetProperty("count").GetInt32());
            return stages["offered"] == 4 && stages["applied"] == 5;
        }, TimeSpan.FromSeconds(60), "funnel counters from the backfill offer");
        stages["enrolled"].Should().Be(1);
        var csv = await owner.GetStringAsync($"{programmes}/{programmeId}/funnel?format=csv");
        csv.Should().StartWith("stage,count,percent_of_applied").And.Contain("applied,5,100");
        var pdf = await owner.GetAsync($"{programmes}/{programmeId}/impact-report");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await pdf.Content.ReadAsStringAsync()).Should().StartWith("%PDF-1.4");

        // Close-out (entry 10): two seats never filled → 2 × (31,500 + 1,000,000 + 12,000) refunded.
        Key(staff);
        var closed = await staff.PostAsync($"/api/v1/admin/sponsorship/programmes/{programmeId}/close", null);
        closed.StatusCode.Should().Be(HttpStatusCode.OK, await closed.Content.ReadAsStringAsync());
        var closeBody = await closed.Content.ReadFromJsonAsync<JsonElement>(Json);
        closeBody.GetProperty("unfilledSeats").GetInt32().Should().Be(2);
        closeBody.GetProperty("unfilledSeatRefund").GetProperty("amount").GetInt64().Should().Be(208_700_000);
        closeBody.GetProperty("remainingFunds").GetProperty("amount").GetInt64().Should().Be(313_050_000 + 1_350_000 - 1_200_000 - 99_999_999 - 3_150_000 - 208_700_000);
        (await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/sponsorship/opportunities?limit=200", Json)).GetProperty("items").EnumerateArray().Should().NotContain(o => o.GetProperty("programmeId").GetGuid() == programmeId);
        tutor.Dispose();
    }
}
