using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Sponsorship.Jobs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

/// <summary>
/// ADR 0021 seat sponsorship end to end: a verified organisation buys seats in a cohort with units (entry 14), gives them to
/// learners by phone (entry 15, Lms enrolment, tutor earnings), an invite converts when the phone registers, open seats go
/// back to the wallet (entry 16), the cohort's seatsLeft follows, staff see the block, and the close job releases what is left.
/// The ledger is shared with the rest of the suite, so money assertions compare deltas on the relevant sub-ledgers.
/// </summary>
[Collection(ApiCollection.Name)]
public class SeatSponsorshipTests(PlastoidsApiFactory factory)
{
    private const long UnitRateKobo = 100_000; // ₦1,000 per unit (Wallet:DefaultRateKobo)
    private const long PriceKobo = 4_500_000; // ₦45,000 per seat (the cohort offering's price)

    private async Task<Money> BalanceAsync(string code, Guid? owner = null)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILedgerService>().GetBalanceAsync(code, owner, CancellationToken.None);
    }

    private static void Key(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected, body);
        return string.IsNullOrEmpty(body) ? default : JsonSerializer.Deserialize<JsonElement>(body, Json);
    }

    private static async Task<long> WalletBalanceAsync(HttpClient client, Guid organisationId)
        => (await client.GetFromJsonAsync<JsonElement>($"/api/v1/wallet/organisations/{organisationId}", Json)).GetProperty("balance").GetInt64();

    /// <summary>A verified tutor publishes a course whose only offering is a cohort (capacity 20, starts in two weeks), then opens a cohort of <paramref name="capacity"/> on it.</summary>
    private async Task<(HttpClient Tutor, Guid TutorUserId, Guid CourseId, Guid OfferingId, Guid CohortId, DateTime EndsAt)> CohortCourseAsync(int capacity)
    {
        var (tutor, tutorUserId, _) = await CatalogueTests.VerifiedTutorAsync(factory);
        var created = await ReadAsync(await tutor.PostAsJsonAsync("/api/v1/catalogue/courses", new { title = $"Cohort course {Guid.NewGuid():N}", summary = "x", level = "beginner", language = "en", category = "data", skills = new[] { "sql" } }), HttpStatusCode.Created);
        var courseId = created.GetProperty("courseId").GetGuid();
        var slug = created.GetProperty("slug").GetString()!;
        var outline = await ReadAsync(await tutor.PutAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/outline", new { summary = "A cohort course long enough to pass the automated summary check with plenty of practical content for learners who want to get started.", modules = new[] { new { title = "Intro", order = 1 } } }), HttpStatusCode.OK);
        var moduleId = outline.GetProperty("modules")[0].GetProperty("id").GetGuid();
        await ReadAsync(await tutor.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/modules/{moduleId}/lessons", new { type = "doc", title = "Read me", order = 1, durationMinutes = 5, isPreview = false, contentJson = (string?)null }), HttpStatusCode.Created);
        var startAt = DateTime.UtcNow.AddDays(14);
        await ReadAsync(await tutor.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/offerings", new { format = "cohort", price = new { amount = PriceKobo, currency = "NGN" }, fundingTypes = new[] { "self", "sponsored" }, capacity = 20, startAt, durationWeeks = 12 }), HttpStatusCode.Created);
        var submit = await ReadAsync(await tutor.PostAsync($"/api/v1/catalogue/courses/{courseId}/submit", null), HttpStatusCode.OK);
        if (submit.GetProperty("requiresHumanReview").GetBoolean())
        {
            var staff = await factory.SignInAsStaffAsync();
            Key(staff);
            (await staff.PostAsJsonAsync($"/api/v1/admin/catalogue/courses/{courseId}/review", new { decision = "approved" })).IsSuccessStatusCode.Should().BeTrue();
        }

        (await tutor.PostAsync($"/api/v1/catalogue/courses/{courseId}/publish", null)).IsSuccessStatusCode.Should().BeTrue();
        var course = await tutor.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/{slug}", Json);
        var offeringId = course.GetProperty("offerings").EnumerateArray().Single(o => o.GetProperty("format").GetString() == "cohort").GetProperty("offeringId").GetGuid();

        var endsAt = startAt.AddDays(84);
        var cohort = await ReadAsync(await tutor.PostAsJsonAsync("/api/v1/lms/cohorts", new { courseId, offeringId, name = "January cohort", startsAt = startAt, endsAt, capacity, programmeId = (Guid?)null }), HttpStatusCode.Created);
        return (tutor, tutorUserId, courseId, offeringId, cohort.GetProperty("cohortId").GetGuid(), endsAt);
    }

    [Fact]
    public async Task Sponsor_buys_seats_allocates_them_releases_the_rest_and_the_cohort_ends()
    {
        var sponsorName = "Lagos Data School " + Guid.NewGuid().ToString("N")[..6];
        var (owner, organisationId, _) = await SponsorshipAndApplicationsTests.VerifiedSponsorAsync(factory, sponsorName);
        await ProviderCoursesTests.EnableMfaAsync(owner);
        var (walletId, _, _) = await new WalletTests(factory).BuyUnitsAsync(owner, 200, organisationId);
        var (_, tutorUserId, courseId, offeringId, cohortId, endsAt) = await CohortCourseAsync(capacity: 5);
        var blocks = $"/api/v1/sponsorship/organisations/{organisationId}/seat-blocks";

        // The public cohort listing shows the price and the seats left before anything is reserved.
        var listing = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/lms/cohorts/{cohortId}"), HttpStatusCode.OK);
        listing.GetProperty("courseId").GetGuid().Should().Be(courseId);
        listing.GetProperty("offeringId").GetGuid().Should().Be(offeringId);
        listing.GetProperty("pricePerSeat").GetProperty("amount").GetInt64().Should().Be(PriceKobo);
        listing.GetProperty("capacity").GetInt32().Should().Be(5);
        listing.GetProperty("seatsLeft").GetInt32().Should().Be(5);
        var upcoming = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/lms/cohorts?courseId={courseId}"), HttpStatusCode.OK);
        upcoming.EnumerateArray().Should().Contain(c => c.GetProperty("cohortId").GetGuid() == cohortId);

        // Buying 3 seats: 3 × ₦45,000 = ₦135,000 = 135 units; entry 14 Dr 2050 (wallet) / Cr 2020 (block).
        Key(owner);
        (await ReadAsync(await owner.PostAsJsonAsync(blocks, new { cohortId, seats = 0 }), HttpStatusCode.UnprocessableEntity)).ToString().Should().Contain("seats_invalid");
        Key(owner);
        (await ReadAsync(await owner.PostAsJsonAsync(blocks, new { cohortId = Guid.NewGuid(), seats = 1 }), HttpStatusCode.NotFound)).ToString().Should().Contain("cohort_not_found");
        Key(owner);
        (await ReadAsync(await owner.PostAsJsonAsync(blocks, new { cohortId, seats = 6 }), HttpStatusCode.Conflict)).ToString().Should().Contain("cohort_full");

        var wallet2050Before = await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId);
        Key(owner);
        var block = await ReadAsync(await owner.PostAsJsonAsync(blocks, new { cohortId, seats = 3 }), HttpStatusCode.Created);
        var blockId = block.GetProperty("blockId").GetGuid();
        block.GetProperty("sponsorName").GetString().Should().Be(sponsorName);
        block.GetProperty("cohortId").GetGuid().Should().Be(cohortId);
        block.GetProperty("courseId").GetGuid().Should().Be(courseId);
        block.GetProperty("seats").GetInt32().Should().Be(3);
        block.GetProperty("open").GetInt32().Should().Be(3);
        block.GetProperty("allocated").GetInt32().Should().Be(0);
        block.GetProperty("pricePerSeat").GetProperty("amount").GetInt64().Should().Be(PriceKobo);
        block.GetProperty("unitsRedeemed").GetInt64().Should().Be(135);
        block.GetProperty("unitRateKobo").GetInt64().Should().Be(UnitRateKobo);
        block.GetProperty("status").GetString().Should().Be("active");
        (await WalletBalanceAsync(owner, organisationId)).Should().Be(65);
        (await BalanceAsync(AccountCodes.ProgrammeFundsHeld, blockId)).Should().Be(Money.Kobo(3 * PriceKobo));
        (wallet2050Before - await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId)).Should().Be(Money.Kobo(3 * PriceKobo));
        (await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/lms/cohorts/{cohortId}"), HttpStatusCode.OK)).GetProperty("seatsLeft").GetInt32().Should().Be(2, "3 of 5 seats are reserved");

        // A learner who already has an account is enrolled at once: entry 15 Dr 2020 (block); Cr 4010 20 %; Cr 2010 (tutor) the rest.
        var learnerPhone = NewPhone();
        var (learner, learnerSignIn) = await factory.SignInWithPhoneAsync(learnerPhone, "Ada Okafor");
        var commission4010Before = await BalanceAsync(AccountCodes.CourseCommission);
        var tutor2010Before = await BalanceAsync(AccountCodes.TutorPayable, tutorUserId);
        Key(owner);
        (await ReadAsync(await owner.PostAsJsonAsync($"{blocks}/{blockId}/allocations", new { displayName = "Nobody" }), HttpStatusCode.UnprocessableEntity)).ToString().Should().Contain("contact_required");
        Key(owner);
        var enrolled = await ReadAsync(await owner.PostAsJsonAsync($"{blocks}/{blockId}/allocations", new { phone = learnerPhone, displayName = "Ada" }), HttpStatusCode.Created);
        enrolled.GetProperty("status").GetString().Should().Be("enrolled");
        enrolled.GetProperty("seatNo").GetInt32().Should().Be(1);
        enrolled.GetProperty("learnerId").GetGuid().Should().Be(learnerSignIn.UserId);
        enrolled.GetProperty("contact").GetString().Should().StartWith("+234").And.Contain("****").And.NotBe(learnerPhone);
        var enrolledAllocationId = enrolled.GetProperty("allocationId").GetGuid();
        (await BalanceAsync(AccountCodes.ProgrammeFundsHeld, blockId)).Should().Be(Money.Kobo(2 * PriceKobo));
        (await BalanceAsync(AccountCodes.CourseCommission) - commission4010Before).Should().Be(Money.Kobo(900_000));
        (await BalanceAsync(AccountCodes.TutorPayable, tutorUserId) - tutor2010Before).Should().Be(Money.Kobo(3_600_000));

        // Lms enrols the learner in the cohort from seat-allocated, labelled with the sponsor; the allocation then learns its enrolment id.
        JsonElement enrolment = default;
        await WaitUntilAsync(async () =>
        {
            var mine = await learner.GetFromJsonAsync<JsonElement>("/api/v1/lms/enrolments/me", Json);
            var match = mine.EnumerateArray().Where(e => e.GetProperty("courseId").GetGuid() == courseId).ToList();
            if (match.Count == 0)
            {
                return false;
            }

            enrolment = match[0];
            return true;
        }, TimeSpan.FromSeconds(60), "the sponsored enrolment from seat-allocated");
        enrolment.GetProperty("cohortId").GetGuid().Should().Be(cohortId);
        enrolment.GetProperty("fundingSource").GetString().Should().Be("sponsored");
        enrolment.GetProperty("fundingLabel").GetString().Should().Be($"Sponsored by {sponsorName}");
        var enrolmentId = enrolment.GetProperty("enrolmentId").GetGuid();
        await WaitUntilAsync(async () =>
        {
            var detail = await owner.GetFromJsonAsync<JsonElement>($"{blocks}/{blockId}", Json);
            var allocation = detail.GetProperty("allocations").EnumerateArray().Single(a => a.GetProperty("allocationId").GetGuid() == enrolledAllocationId);
            return allocation.TryGetProperty("enrolmentId", out var id) && id.ValueKind == JsonValueKind.String && id.GetGuid() == enrolmentId;
        }, TimeSpan.FromSeconds(60), "the allocation linked to its enrolment");

        // A phone with no account is invited; the invite converts as soon as that phone signs up.
        var newPhone = NewPhone();
        Key(owner);
        var invited = await ReadAsync(await owner.PostAsJsonAsync($"{blocks}/{blockId}/allocations", new { phone = newPhone }), HttpStatusCode.Created);
        invited.GetProperty("status").GetString().Should().Be("invited");
        invited.GetProperty("seatNo").GetInt32().Should().Be(2);
        invited.GetProperty("learnerId").ValueKind.Should().Be(JsonValueKind.Null);
        var invitedAllocationId = invited.GetProperty("allocationId").GetGuid();
        var detailBefore = await owner.GetFromJsonAsync<JsonElement>($"{blocks}/{blockId}", Json);
        detailBefore.GetProperty("invited").GetInt32().Should().Be(1);
        detailBefore.GetProperty("enrolled").GetInt32().Should().Be(1);
        detailBefore.GetProperty("open").GetInt32().Should().Be(1);
        detailBefore.GetProperty("walletBalance").GetInt64().Should().Be(65);

        var (newLearner, newSignIn) = await factory.SignInWithPhoneAsync(newPhone, "Bola Adeyemi");
        await WaitUntilAsync(async () =>
        {
            var detail = await owner.GetFromJsonAsync<JsonElement>($"{blocks}/{blockId}", Json);
            var allocation = detail.GetProperty("allocations").EnumerateArray().Single(a => a.GetProperty("allocationId").GetGuid() == invitedAllocationId);
            return allocation.GetProperty("status").GetString() == "enrolled" && allocation.GetProperty("learnerId").GetGuid() == newSignIn.UserId;
        }, TimeSpan.FromSeconds(60), "the invited seat converted on sign-up");
        // The cohort row is updated again when Lms admits the converted seat; wait for that enrolment so the release below does not race it.
        await WaitUntilAsync(async () => (await newLearner.GetFromJsonAsync<JsonElement>("/api/v1/lms/enrolments/me", Json)).EnumerateArray().Any(e => e.TryGetProperty("cohortId", out var c) && c.ValueKind == JsonValueKind.String && c.GetGuid() == cohortId), TimeSpan.FromSeconds(60), "the converted seat is enrolled in the cohort");
        (await BalanceAsync(AccountCodes.ProgrammeFundsHeld, blockId)).Should().Be(Money.Kobo(PriceKobo));
        (await BalanceAsync(AccountCodes.TutorPayable, tutorUserId) - tutor2010Before).Should().Be(Money.Kobo(2 * 3_600_000));

        // One allocation per learner per block.
        Key(owner);
        (await ReadAsync(await owner.PostAsJsonAsync($"{blocks}/{blockId}/allocations", new { phone = learnerPhone }), HttpStatusCode.Conflict)).ToString().Should().Contain("seat_already_allocated");

        // Releasing the last open seat: units back pro rata (45 of 135), entry 16 Dr 2020 (block) / Cr 2050 (wallet).
        Key(owner);
        (await ReadAsync(await owner.PostAsJsonAsync($"{blocks}/{blockId}/release", new { seats = 2 }), HttpStatusCode.Conflict)).ToString().Should().Contain("release_exceeds_open");
        Key(owner);
        var released = await ReadAsync(await owner.PostAsJsonAsync($"{blocks}/{blockId}/release", new { seats = 1 }), HttpStatusCode.OK);
        released.GetProperty("released").GetInt32().Should().Be(1);
        released.GetProperty("open").GetInt32().Should().Be(0);
        released.GetProperty("allocated").GetInt32().Should().Be(2);
        released.GetProperty("enrolled").GetInt32().Should().Be(2);
        (await WalletBalanceAsync(owner, organisationId)).Should().Be(110);
        (await BalanceAsync(AccountCodes.ProgrammeFundsHeld, blockId)).Should().Be(Money.Zero());
        (wallet2050Before - await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId)).Should().Be(Money.Kobo(2 * PriceKobo));
        Key(owner);
        (await ReadAsync(await owner.PostAsJsonAsync($"{blocks}/{blockId}/allocations", new { phone = NewPhone() }), HttpStatusCode.Conflict)).ToString().Should().Contain("no_open_seats");

        // The cohort: 2 enrolled, nothing reserved → 3 of 5 left.
        await WaitUntilAsync(async () =>
        {
            var c = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/lms/cohorts/{cohortId}", Json);
            return c.GetProperty("enrolled").GetInt32() == 2 && c.GetProperty("reserved").GetInt32() == 0 && c.GetProperty("seatsLeft").GetInt32() == 3;
        }, TimeSpan.FromSeconds(60), "cohort seats to settle at 2 enrolled, 0 reserved");

        // The sponsor's list and the staff view.
        var list = await owner.GetFromJsonAsync<JsonElement>(blocks, Json);
        list.EnumerateArray().Should().ContainSingle(b => b.GetProperty("blockId").GetGuid() == blockId);
        var staff = await factory.SignInAsStaffAsync();
        var adminList = await ReadAsync(await staff.GetAsync("/api/v1/admin/sponsorship/seat-blocks?status=active&limit=100"), HttpStatusCode.OK);
        adminList.EnumerateArray().Should().Contain(b => b.GetProperty("blockId").GetGuid() == blockId);
        var adminDetail = await ReadAsync(await staff.GetAsync($"/api/v1/admin/sponsorship/seat-blocks/{blockId}"), HttpStatusCode.OK);
        adminDetail.GetProperty("organisationId").GetGuid().Should().Be(organisationId);
        adminDetail.GetProperty("allocations").GetArrayLength().Should().Be(2);
        (await owner.GetAsync("/api/v1/admin/sponsorship/seat-blocks")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A second block with an open seat: when the cohort ends, the nightly job releases it (closeout) and closes both blocks.
        Key(owner);
        var second = await ReadAsync(await owner.PostAsJsonAsync(blocks, new { cohortId, seats = 1 }), HttpStatusCode.Created);
        var secondId = second.GetProperty("blockId").GetGuid();
        (await WalletBalanceAsync(owner, organisationId)).Should().Be(65);
        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<SeatBlockCloseJob>();
            (await job.RunAsync(endsAt.AddDays(-1), CancellationToken.None)).Should().Be(0, "the cohort has not ended");
            (await job.RunAsync(endsAt.AddDays(1), CancellationToken.None)).Should().BeGreaterThanOrEqualTo(2);
        }

        (await WalletBalanceAsync(owner, organisationId)).Should().Be(110);
        (await BalanceAsync(AccountCodes.ProgrammeFundsHeld, secondId)).Should().Be(Money.Zero());
        var closed = await owner.GetFromJsonAsync<JsonElement>($"{blocks}/{secondId}", Json);
        closed.GetProperty("status").GetString().Should().Be("closed");
        closed.GetProperty("released").GetInt32().Should().Be(1);
        closed.GetProperty("closedAt").ValueKind.Should().Be(JsonValueKind.String);
        (await owner.GetFromJsonAsync<JsonElement>($"{blocks}/{blockId}", Json)).GetProperty("status").GetString().Should().Be("closed");
        Key(owner);
        (await ReadAsync(await owner.PostAsJsonAsync($"{blocks}/{secondId}/release", new { seats = 1 }), HttpStatusCode.Conflict)).ToString().Should().Contain("block_closed");
    }
}
