using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Payouts.Jobs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

/// <summary>
/// ADR 0019 money path: a learner buys an organisation-owned course taught by an engaged tutor on a 40% revenue share.
/// ₦45,000 at the course_provider rate (15%) → ₦6,750 commission, ₦15,300 tutor share, ₦22,950 to the organisation (2060);
/// a 10% refund reverses all three pro rata; both payees see earnings and the organisation can register a payout account.
/// Ledger balances are shared with the rest of the suite, so every assertion compares deltas on the sub-ledgers.
/// </summary>
[Collection(ApiCollection.Name)]
public class ProviderSalesTests(PlastoidsApiFactory factory)
{
    private async Task<Money> BalanceAsync(string code, Guid? owner = null)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILedgerService>().GetBalanceAsync(code, owner, CancellationToken.None);
    }

    private static HttpRequestMessage SignedWebhook(string payload, string signature) =>
        new(HttpMethod.Post, "/api/v1/payments/webhooks/fake") { Content = new StringContent(payload, Encoding.UTF8, "application/json"), Headers = { { "x-paystack-signature", signature } } };

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

    /// <summary>A verified organisation with a published self-paced course (₦45,000) taught by an engaged verified tutor on a 40% revenue share.</summary>
    internal static async Task<(HttpClient Owner, Guid OrganisationId, HttpClient Tutor, Guid TutorUserId, string Slug, Guid CourseId)> PublishedOrganisationCourseAsync(PlastoidsApiFactory factory, int shareBps = 4000)
    {
        var (owner, organisationId, _) = await SponsorshipAndApplicationsTests.VerifiedSponsorAsync(factory, "Provider " + Guid.NewGuid().ToString("N")[..6]);
        await ProviderCoursesTests.EnableMfaAsync(owner); // authoring endpoints need an MFA session (ADR 0019 §2)
        var (tutor, tutorUserId, tutorId) = await CatalogueTests.VerifiedTutorAsync(factory);

        var created = await ReadAsync(await owner.PostAsJsonAsync($"/api/v1/catalogue/organisations/{organisationId}/courses", new { title = $"Provider course {Guid.NewGuid():N}", summary = "x", level = "beginner", language = "en", category = "data", skills = new[] { "sql" } }), HttpStatusCode.Created);
        var courseId = created.GetProperty("courseId").GetGuid();
        var slug = created.GetProperty("slug").GetString()!;
        var outline = await ReadAsync(await owner.PutAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/outline", new { summary = "A course long enough to pass the automated summary check with plenty of practical content for learners who want to get started.", modules = new[] { new { title = "Intro", order = 1 } } }), HttpStatusCode.OK);
        var moduleId = outline.GetProperty("modules")[0].GetProperty("id").GetGuid();
        await ReadAsync(await owner.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/modules/{moduleId}/lessons", new { type = "doc", title = "Read me", order = 1, durationMinutes = 5, isPreview = false, contentJson = (string?)null }), HttpStatusCode.Created);

        // Engage the tutor: offered by the organisation, accepted by the tutor.
        var offered = await ReadAsync(await owner.PostAsJsonAsync($"/api/v1/catalogue/organisations/{organisationId}/courses/{courseId}/engagements", new { tutorId, terms = new { kind = "revenue_share", shareBps }, note = "Welcome aboard" }), HttpStatusCode.Created);
        var engagementId = offered.GetProperty("engagementId").GetGuid();
        offered.GetProperty("status").GetString().Should().Be("offered");
        var accepted = await ReadAsync(await tutor.PostAsJsonAsync($"/api/v1/catalogue/engagements/{engagementId}/respond", new { accept = true }), HttpStatusCode.OK);
        accepted.GetProperty("status").GetString().Should().Be("active");

        await ReadAsync(await owner.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/offerings", new { format = "self_paced", price = new { amount = 4_500_000, currency = "NGN" }, fundingTypes = new[] { "self" }, capacity = (int?)null, startAt = (DateTime?)null, durationWeeks = 4, tutorUserId }), HttpStatusCode.Created);
        var submit = await ReadAsync(await owner.PostAsync($"/api/v1/catalogue/courses/{courseId}/submit", null), HttpStatusCode.OK);
        if (submit.GetProperty("requiresHumanReview").GetBoolean())
        {
            var staff = await factory.SignInAsStaffAsync();
            Key(staff);
            await ReadAsync(await staff.PostAsJsonAsync($"/api/v1/admin/catalogue/courses/{courseId}/review", new { decision = "approved" }), HttpStatusCode.OK);
        }

        await ReadAsync(await owner.PostAsync($"/api/v1/catalogue/courses/{courseId}/publish", null), HttpStatusCode.OK);
        return (owner, organisationId, tutor, tutorUserId, slug, courseId);
    }

    [Fact]
    public async Task Sale_of_an_organisation_course_splits_three_ways_refunds_pro_rata_and_pays_the_organisation()
    {
        var (owner, organisationId, tutor, tutorUserId, slug, _) = await PublishedOrganisationCourseAsync(factory);
        var (learner, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Provider buyer");
        var detail = await learner.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/{slug}", Json);
        detail.GetProperty("ownerType").GetString().Should().Be("organisation");
        detail.GetProperty("provider").GetProperty("organisationId").GetGuid().Should().Be(organisationId);
        var quoteId = detail.GetProperty("offerings")[0].GetProperty("quoteId").GetString()!;

        var before2060 = await BalanceAsync(AccountCodes.ProviderPayable, organisationId);
        var before2010 = await BalanceAsync(AccountCodes.TutorPayable, tutorUserId);
        var before4010 = await BalanceAsync(AccountCodes.CourseCommission);
        var before1030 = await BalanceAsync(AccountCodes.GatewayReceivable);

        Key(learner);
        var intent = await ReadAsync(await learner.PostAsJsonAsync("/api/v1/payments/intents", new { purpose = "course", quoteId }), HttpStatusCode.Created);
        var intentId = intent.GetProperty("paymentIntentId").GetGuid();
        intent.GetProperty("commissionBps").GetInt32().Should().Be(1500, "course_provider fee code");
        intent.GetProperty("payeeType").GetString().Should().Be("organisation");
        intent.GetProperty("payeeOrganisationId").GetGuid().Should().Be(organisationId);
        intent.GetProperty("tutorShare").GetProperty("amount").GetInt64().Should().Be(1_530_000, "40% of the ₦38,250 net");

        var gateway = factory.Services.GetRequiredService<FakePaymentGateway>();
        var (payload, signature) = gateway.BuildWebhook("charge.success", intent.GetProperty("gatewayReference").GetString()!, Money.NairaAmount(45_000), DateTime.UtcNow);
        (await ReadAsync(await factory.CreateClient().SendAsync(SignedWebhook(payload, signature)), HttpStatusCode.OK)).GetProperty("outcome").GetString().Should().Be("confirmed");

        (await BalanceAsync(AccountCodes.GatewayReceivable) - before1030).Should().Be(Money.NairaAmount(45_000));
        (await BalanceAsync(AccountCodes.CourseCommission) - before4010).Should().Be(Money.NairaAmount(6_750));
        (await BalanceAsync(AccountCodes.TutorPayable, tutorUserId) - before2010).Should().Be(Money.NairaAmount(15_300));
        (await BalanceAsync(AccountCodes.ProviderPayable, organisationId) - before2060).Should().Be(Money.NairaAmount(22_950));

        // Both payees see the sale as a held earning.
        await WaitUntilAsync(async () => (await owner.GetFromJsonAsync<JsonElement>($"/api/v1/payouts/organisations/{organisationId}/earnings", Json)).GetProperty("gross").GetProperty("amount").GetInt64() >= 4_500_000, TimeSpan.FromSeconds(60), "organisation earning from payment-succeeded");
        var orgEarnings = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/payouts/organisations/{organisationId}/earnings", Json);
        orgEarnings.GetProperty("net").GetProperty("amount").GetInt64().Should().Be(2_295_000);
        orgEarnings.GetProperty("payoutAccount").ValueKind.Should().Be(JsonValueKind.Null);
        await WaitUntilAsync(async () => (await tutor.GetFromJsonAsync<JsonElement>("/api/v1/payouts/me", Json)).GetProperty("gross").GetProperty("amount").GetInt64() >= 4_500_000, TimeSpan.FromSeconds(60), "tutor earning from payment-succeeded");
        (await tutor.GetFromJsonAsync<JsonElement>("/api/v1/payouts/me", Json)).GetProperty("net").GetProperty("amount").GetInt64().Should().Be(1_530_000);

        // Entry 5: a 10% refund reverses commission, tutor share and organisation share pro rata.
        var staff = await factory.SignInAsStaffAsync();
        Key(staff);
        var refund = await ReadAsync(await staff.PostAsJsonAsync($"/api/v1/admin/payments/intents/{intentId}/refunds", new { amount = new { amount = 450_000, currency = "NGN" }, reason = "Lesson 1 was missing" }), HttpStatusCode.Created);
        refund.GetProperty("status").GetString().Should().Be("issued");
        (await BalanceAsync(AccountCodes.CourseCommission) - before4010).Should().Be(Money.NairaAmount(6_750 - 675));
        (await BalanceAsync(AccountCodes.TutorPayable, tutorUserId) - before2010).Should().Be(Money.NairaAmount(15_300 - 1_530));
        (await BalanceAsync(AccountCodes.ProviderPayable, organisationId) - before2060).Should().Be(Money.NairaAmount(22_950 - 2_295));
        await WaitUntilAsync(async () => (await owner.GetFromJsonAsync<JsonElement>($"/api/v1/payouts/organisations/{organisationId}/earnings", Json)).GetProperty("net").GetProperty("amount").GetInt64() == 2_065_500, TimeSpan.FromSeconds(60), "refund applied to the organisation earning");
        await WaitUntilAsync(async () => (await tutor.GetFromJsonAsync<JsonElement>("/api/v1/payouts/me", Json)).GetProperty("net").GetProperty("amount").GetInt64() == 1_377_000, TimeSpan.FromSeconds(60), "refund applied to the tutor earning");

        // Organisation payout account: the bank name must match the registered organisation name; finance or owner only.
        var resolver = factory.Services.GetRequiredService<FakeBankAccountResolver>();
        var mismatch = "02" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var matched = "03" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var organisationName = (await owner.GetFromJsonAsync<JsonElement>("/api/v1/identity/me", Json)).GetProperty("organisations").EnumerateArray().Single(o => o.GetProperty("id").GetGuid() == organisationId).GetProperty("name").GetString()!;
        resolver.KnownAccounts[matched] = organisationName.ToUpperInvariant() + " LTD";
        (await owner.PutAsJsonAsync($"/api/v1/payouts/organisations/{organisationId}/payout-account", new { bankCode = "058", accountNumber = mismatch })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "FAKE ACCOUNT does not match the organisation name");
        (await learner.PutAsJsonAsync($"/api/v1/payouts/organisations/{organisationId}/payout-account", new { bankCode = "058", accountNumber = matched })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var account = await ReadAsync(await owner.PutAsJsonAsync($"/api/v1/payouts/organisations/{organisationId}/payout-account", new { bankCode = "058", accountNumber = matched }), HttpStatusCode.OK);
        account.GetProperty("nameMatched").GetBoolean().Should().BeTrue();
        account.GetProperty("accountNumberMasked").GetString().Should().EndWith(matched[^4..]);

        // Release the holds (0 days in tests) and draft a batch: the organisation is paid from 2060, the tutor from 2010.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EarningsReleaseJob>().RunAsync(DateTime.UtcNow.AddMinutes(1), CancellationToken.None);
        }

        var statement = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/payouts/organisations/{organisationId}/earnings", Json);
        statement.GetProperty("available").GetProperty("amount").GetInt64().Should().Be(2_065_500);
        statement.GetProperty("payoutAccount").GetProperty("nameMatched").GetBoolean().Should().BeTrue();

        Key(staff);
        var drafted = await staff.PostAsync("/api/v1/admin/payouts/batches", null);
        var draft = await ReadAsync(drafted, HttpStatusCode.Created);
        var orgPayout = draft.GetProperty("payouts").EnumerateArray().Single(p => p.GetProperty("payeeType").GetString() == "organisation" && p.GetProperty("payeeId").GetGuid() == organisationId);
        orgPayout.GetProperty("amount").GetProperty("amount").GetInt64().Should().Be(2_065_500);
        var batchId = draft.GetProperty("batchId").GetGuid();
        Key(staff);
        (await staff.PostAsJsonAsync($"/api/v1/admin/payouts/batches/{batchId}/reject", new { reason = "Drafted by the provider-sales test" })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Paying_an_organisation_course_with_units_posts_the_provider_variant_of_entry_12()
    {
        var (_, organisationId, _, tutorUserId, slug, _) = await PublishedOrganisationCourseAsync(factory, shareBps: 5000);
        var (learner, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Unit buyer");
        var walletTests = new WalletTests(factory);
        await walletTests.BuyUnitsAsync(learner, 50);

        var detail = await learner.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/{slug}", Json);
        var quoteId = detail.GetProperty("offerings")[0].GetProperty("quoteId").GetString()!;
        var before2060 = await BalanceAsync(AccountCodes.ProviderPayable, organisationId);
        var before2010 = await BalanceAsync(AccountCodes.TutorPayable, tutorUserId);

        Key(learner);
        var intent = await ReadAsync(await learner.PostAsJsonAsync("/api/v1/payments/intents", new { purpose = "course", quoteId, payWithUnits = true }), HttpStatusCode.Created);
        intent.GetProperty("status").GetString().Should().Be("succeeded");
        intent.GetProperty("unitsCharged").GetInt64().Should().Be(45);
        // ₦45,000 − 15% = ₦38,250 net; half to the tutor, half to the organisation.
        (await BalanceAsync(AccountCodes.TutorPayable, tutorUserId) - before2010).Should().Be(Money.NairaAmount(19_125));
        (await BalanceAsync(AccountCodes.ProviderPayable, organisationId) - before2060).Should().Be(Money.NairaAmount(19_125));
    }
}
