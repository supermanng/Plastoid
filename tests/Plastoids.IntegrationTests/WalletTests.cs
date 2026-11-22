using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

/// <summary>
/// ADR 0018 units wallet: buying units (entry 11), paying a course with units (entry 12), refunds back to units (entry 5, units
/// variant), funding a programme from an organisation wallet (entry 6, units variant), the four-eyes unit rate and staff adjustments.
/// The ledger is shared with the rest of the suite, so every money assertion compares deltas on the wallet / tutor sub-ledgers.
/// </summary>
[Collection(ApiCollection.Name)]
public class WalletTests(PlastoidsApiFactory factory)
{
    private const long DefaultRateKobo = 100_000; // ₦1,000 per unit (Wallet:DefaultRateKobo)

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

    /// <summary>Buys <paramref name="units"/> for the signed-in client (optionally for an organisation) and confirms the charge through the fake gateway; returns the wallet id once the consumer credited it.</summary>
    private async Task<(Guid WalletId, Guid PaymentIntentId, long AmountKobo)> BuyUnitsAsync(HttpClient client, long units, Guid? organisationId = null)
    {
        var walletPath = organisationId is null ? "/api/v1/wallet/me" : $"/api/v1/wallet/organisations/{organisationId}";
        var before = (await client.GetFromJsonAsync<JsonElement>(walletPath, Json)).GetProperty("balance").GetInt64();

        Key(client);
        var created = await ReadAsync(await client.PostAsJsonAsync("/api/v1/wallet/purchases", new { units, organisationId }), HttpStatusCode.Created);
        created.GetProperty("units").GetInt64().Should().Be(units);
        created.GetProperty("rateKobo").GetInt64().Should().Be(DefaultRateKobo);
        var amountKobo = created.GetProperty("amount").GetProperty("amount").GetInt64();
        amountKobo.Should().Be(units * DefaultRateKobo);
        created.GetProperty("status").GetString().Should().Be("pending");
        created.GetProperty("checkoutUrl").GetString().Should().NotBeNullOrEmpty("a unit purchase goes through the hosted checkout");
        var intentId = created.GetProperty("paymentIntentId").GetGuid();
        var walletId = created.GetProperty("walletId").GetGuid();

        var intent = await client.GetFromJsonAsync<JsonElement>($"/api/v1/payments/intents/{intentId}", Json);
        intent.GetProperty("purpose").GetString().Should().Be("units");
        var reference = intent.GetProperty("gatewayReference").GetString()!;

        var gateway = factory.Services.GetRequiredService<FakePaymentGateway>();
        var (payload, signature) = gateway.BuildWebhook("charge.success", reference, Money.Kobo(amountKobo), DateTime.UtcNow);
        var confirmed = await ReadAsync(await factory.CreateClient().SendAsync(SignedWebhook(payload, signature)), HttpStatusCode.OK);
        confirmed.GetProperty("outcome").GetString().Should().Be("confirmed");

        await WaitUntilAsync(async () => (await client.GetFromJsonAsync<JsonElement>(walletPath, Json)).GetProperty("balance").GetInt64() == before + units, TimeSpan.FromSeconds(60), $"{units} units credited from payment-succeeded");
        return (walletId, intentId, amountKobo);
    }

    private async Task<(HttpClient Client, Guid UserId, Guid WalletId)> FundedLearnerAsync(long units)
    {
        var (learner, signIn) = await factory.SignInWithPhoneAsync(NewPhone(), "Unit buyer");
        var (walletId, _, _) = await BuyUnitsAsync(learner, units);
        return (learner, signIn.UserId, walletId);
    }

    [Fact]
    public async Task Unit_rate_is_public_and_defaults_until_staff_approve_one()
    {
        var rate = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/wallet/rate", Json);
        rate.GetProperty("rateKobo").GetInt64().Should().Be(DefaultRateKobo);
        rate.GetProperty("rateValue").GetProperty("amount").GetInt64().Should().Be(DefaultRateKobo);
        rate.GetProperty("source").GetString().Should().Be("default");
        rate.GetProperty("version").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Buying_units_credits_a_lot_when_the_gateway_confirms_and_posts_entry_11()
    {
        var (learner, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Unit buyer");
        var empty = await learner.GetFromJsonAsync<JsonElement>("/api/v1/wallet/me", Json);
        empty.GetProperty("balance").GetInt64().Should().Be(0);
        empty.GetProperty("ownerType").GetString().Should().Be("user");

        Key(learner);
        var created = await ReadAsync(await learner.PostAsJsonAsync("/api/v1/wallet/purchases", new { units = 50 }), HttpStatusCode.Created);
        var intentId = created.GetProperty("paymentIntentId").GetGuid();
        var walletId = created.GetProperty("walletId").GetGuid();
        var amountKobo = created.GetProperty("amount").GetProperty("amount").GetInt64();
        amountKobo.Should().Be(50 * DefaultRateKobo);
        created.GetProperty("status").GetString().Should().Be("pending");

        var pendingView = await learner.GetFromJsonAsync<JsonElement>("/api/v1/wallet/me", Json);
        pendingView.GetProperty("pendingPurchases").EnumerateArray().Should().ContainSingle(p => p.GetProperty("paymentIntentId").GetGuid() == intentId);
        pendingView.GetProperty("balance").GetInt64().Should().Be(0, "nothing is credited before the gateway confirms");

        var before2050 = await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId);
        var before1030 = await BalanceAsync(AccountCodes.GatewayReceivable);

        var reference = (await learner.GetFromJsonAsync<JsonElement>($"/api/v1/payments/intents/{intentId}", Json)).GetProperty("gatewayReference").GetString()!;
        var gateway = factory.Services.GetRequiredService<FakePaymentGateway>();
        var (payload, signature) = gateway.BuildWebhook("charge.success", reference, Money.Kobo(amountKobo), DateTime.UtcNow);
        var confirmed = await ReadAsync(await factory.CreateClient().SendAsync(SignedWebhook(payload, signature)), HttpStatusCode.OK);
        confirmed.GetProperty("outcome").GetString().Should().Be("confirmed");

        await WaitUntilAsync(async () => (await learner.GetFromJsonAsync<JsonElement>("/api/v1/wallet/me", Json)).GetProperty("balance").GetInt64() == 50, TimeSpan.FromSeconds(60), "50 units credited from payment-succeeded");

        // Entry 11: Dr 1030, Cr 2050 (this wallet's sub-ledger). No revenue, no payee.
        (await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId) - before2050).Should().Be(Money.Kobo(amountKobo));
        (await BalanceAsync(AccountCodes.GatewayReceivable) - before1030).Should().Be(Money.Kobo(amountKobo));

        var view = await learner.GetFromJsonAsync<JsonElement>("/api/v1/wallet/me", Json);
        view.GetProperty("walletId").GetGuid().Should().Be(walletId);
        view.GetProperty("balanceValue").GetProperty("amount").GetInt64().Should().Be(amountKobo);
        view.GetProperty("lifetimePurchased").GetInt64().Should().Be(50);
        var lots = view.GetProperty("openLots").EnumerateArray().ToList();
        lots.Should().HaveCount(1);
        lots[0].GetProperty("unitsTotal").GetInt64().Should().Be(50);
        lots[0].GetProperty("unitsRemaining").GetInt64().Should().Be(50);
        lots[0].GetProperty("valueRemaining").GetProperty("amount").GetInt64().Should().Be(amountKobo);
        lots[0].GetProperty("kind").GetString().Should().Be("purchase");
        var recent = view.GetProperty("recentTransactions").EnumerateArray().ToList();
        recent[0].GetProperty("kind").GetString().Should().Be("purchase");
        recent[0].GetProperty("units").GetInt64().Should().Be(50);
        recent[0].GetProperty("balanceAfter").GetInt64().Should().Be(50);
        view.GetProperty("pendingPurchases").EnumerateArray().Should().BeEmpty("the purchase is credited");

        var page = await learner.GetFromJsonAsync<JsonElement>("/api/v1/wallet/me/transactions?limit=10", Json);
        page.GetProperty("items").EnumerateArray().Should().ContainSingle(t => t.GetProperty("kind").GetString() == "purchase");

        // Replaying the webhook never credits twice.
        (await ReadAsync(await factory.CreateClient().SendAsync(SignedWebhook(payload, signature)), HttpStatusCode.OK)).GetProperty("outcome").GetString().Should().Be("duplicate");
        await Task.Delay(500);
        (await learner.GetFromJsonAsync<JsonElement>("/api/v1/wallet/me", Json)).GetProperty("balance").GetInt64().Should().Be(50);
    }

    [Fact]
    public async Task Paying_a_course_with_units_settles_at_once_posts_entry_12_and_enrols_the_learner()
    {
        var (tutor, tutorUserId, slug, courseId) = await CatalogueTests.PublishedCourseAsync(factory);
        var (learner, _, walletId) = await FundedLearnerAsync(60);
        var detail = await learner.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/{slug}", Json);
        var quoteId = detail.GetProperty("offerings")[0].GetProperty("quoteId").GetString()!;

        var before2050 = await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId);
        var before2010 = await BalanceAsync(AccountCodes.TutorPayable, tutorUserId);
        var before4010 = await BalanceAsync(AccountCodes.CourseCommission);
        var before1030 = await BalanceAsync(AccountCodes.GatewayReceivable);

        Key(learner);
        var paid = await ReadAsync(await learner.PostAsJsonAsync("/api/v1/payments/intents", new { purpose = "course", quoteId, payWithUnits = true }), HttpStatusCode.Created);
        paid.GetProperty("status").GetString().Should().Be("succeeded");
        paid.GetProperty("paymentMethod").GetString().Should().Be("units");
        paid.GetProperty("unitsCharged").GetInt64().Should().Be(45, "₦45,000 at ₦1,000 per unit");
        paid.GetProperty("unitRateKobo").GetInt64().Should().Be(DefaultRateKobo);
        paid.GetProperty("checkoutUrl").ValueKind.Should().Be(JsonValueKind.Null);
        paid.GetProperty("amount").GetProperty("amount").GetInt64().Should().Be(4_500_000);
        paid.GetProperty("commissionBps").GetInt32().Should().Be(2000);
        var intentId = paid.GetProperty("paymentIntentId").GetGuid();

        var wallet = await learner.GetFromJsonAsync<JsonElement>("/api/v1/wallet/me", Json);
        wallet.GetProperty("balance").GetInt64().Should().Be(15);
        wallet.GetProperty("lifetimeRedeemed").GetInt64().Should().Be(45);
        var last = wallet.GetProperty("recentTransactions")[0];
        last.GetProperty("kind").GetString().Should().Be("redemption");
        last.GetProperty("units").GetInt64().Should().Be(-45);
        last.GetProperty("value").GetProperty("amount").GetInt64().Should().Be(4_500_000);
        last.GetProperty("purpose").GetString().Should().Be("course");
        wallet.GetProperty("openLots")[0].GetProperty("unitsRemaining").GetInt64().Should().Be(15);
        wallet.GetProperty("openLots")[0].GetProperty("valueRemaining").GetProperty("amount").GetInt64().Should().Be(1_500_000);

        // Entry 12: Dr 2050 (wallet) 45,000; Cr 2010 (tutor) 36,000; Cr 4010 9,000. Nothing touches the gateway receivable.
        (await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId) - before2050).Should().Be(Money.NairaAmount(-45_000));
        (await BalanceAsync(AccountCodes.TutorPayable, tutorUserId) - before2010).Should().Be(Money.NairaAmount(36_000));
        (await BalanceAsync(AccountCodes.CourseCommission) - before4010).Should().Be(Money.NairaAmount(9_000));
        (await BalanceAsync(AccountCodes.GatewayReceivable) - before1030).Should().Be(Money.Zero());

        // The same payments.payment-succeeded.v1 event enrols the learner, as a card payment would.
        await WaitUntilAsync(async () =>
        {
            var mine = await learner.GetFromJsonAsync<JsonElement>("/api/v1/lms/enrolments/me", Json);
            var items = mine.ValueKind == JsonValueKind.Array ? mine.EnumerateArray() : mine.GetProperty("items").EnumerateArray();
            return items.Any(e => e.TryGetProperty("courseId", out var c) && c.GetGuid() == courseId);
        }, TimeSpan.FromSeconds(60), "enrolment from the units payment");

        var mine = await learner.GetFromJsonAsync<JsonElement>("/api/v1/payments/me", Json);
        mine.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("paymentIntentId").GetGuid()).Should().Contain(intentId);
        _ = tutor;
    }

    [Fact]
    public async Task A_units_paid_intent_can_be_read_back()
    {
        var (_, _, slug, _) = await CatalogueTests.PublishedCourseAsync(factory);
        var (learner, _, _) = await FundedLearnerAsync(45);
        var quoteId = (await learner.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/{slug}", Json)).GetProperty("offerings")[0].GetProperty("quoteId").GetString()!;
        Key(learner);
        var paid = await ReadAsync(await learner.PostAsJsonAsync("/api/v1/payments/intents", new { purpose = "course", quoteId, payWithUnits = true }), HttpStatusCode.Created);
        var intentId = paid.GetProperty("paymentIntentId").GetGuid();

        var view = await ReadAsync(await learner.GetAsync($"/api/v1/payments/intents/{intentId}"), HttpStatusCode.OK);
        view.GetProperty("status").GetString().Should().Be("succeeded");
        view.GetProperty("gateway").GetString().Should().Be("units");
        var receipt = await ReadAsync(await learner.GetAsync($"/api/v1/payments/intents/{intentId}/receipt"), HttpStatusCode.OK);
        receipt.GetProperty("receiptNumber").GetString().Should().StartWith("RCP-");
    }

    [Fact]
    public async Task Paying_with_too_few_units_is_refused_and_leaves_the_wallet_untouched()
    {
        var (_, _, slug, _) = await CatalogueTests.PublishedCourseAsync(factory);
        var (learner, _, walletId) = await FundedLearnerAsync(10);
        var quoteId = (await learner.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/{slug}", Json)).GetProperty("offerings")[0].GetProperty("quoteId").GetString()!;
        var before2050 = await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId);

        Key(learner);
        var refused = await ReadAsync(await learner.PostAsJsonAsync("/api/v1/payments/intents", new { purpose = "course", quoteId, payWithUnits = true }), HttpStatusCode.Conflict);
        refused.GetProperty("code").GetString().Should().Be("insufficient_units");
        refused.GetProperty("detail").GetString().Should().Contain("35 more units");

        var wallet = await learner.GetFromJsonAsync<JsonElement>("/api/v1/wallet/me", Json);
        wallet.GetProperty("balance").GetInt64().Should().Be(10);
        wallet.GetProperty("recentTransactions").EnumerateArray().Should().OnlyContain(t => t.GetProperty("kind").GetString() == "purchase");
        (await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId) - before2050).Should().Be(Money.Zero(), "the failed sale rolled back");
        var payments = await learner.GetFromJsonAsync<JsonElement>("/api/v1/payments/me", Json);
        payments.GetProperty("items").EnumerateArray().Should().OnlyContain(i => i.GetProperty("purpose").GetString() == "units", "no course intent survives the rollback");
    }

    [Fact]
    public async Task Refunding_a_units_payment_returns_units_pro_rata_to_the_wallet()
    {
        var (_, tutorUserId, slug, _) = await CatalogueTests.PublishedCourseAsync(factory);
        var (learner, _, walletId) = await FundedLearnerAsync(45);
        var quoteId = (await learner.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/{slug}", Json)).GetProperty("offerings")[0].GetProperty("quoteId").GetString()!;
        Key(learner);
        var paid = await ReadAsync(await learner.PostAsJsonAsync("/api/v1/payments/intents", new { purpose = "course", quoteId, payWithUnits = true }), HttpStatusCode.Created);
        var intentId = paid.GetProperty("paymentIntentId").GetGuid();
        (await learner.GetFromJsonAsync<JsonElement>("/api/v1/wallet/me", Json)).GetProperty("balance").GetInt64().Should().Be(0);

        var before2050 = await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId);
        var before2010 = await BalanceAsync(AccountCodes.TutorPayable, tutorUserId);
        var before4010 = await BalanceAsync(AccountCodes.CourseCommission);
        var before1020 = await BalanceAsync(AccountCodes.BankClientFunds);

        var staff = await factory.SignInAsStaffAsync();
        Key(staff);
        var refund = await ReadAsync(await staff.PostAsJsonAsync($"/api/v1/admin/payments/intents/{intentId}/refunds", new { amount = new { amount = 900_000, currency = "NGN" }, reason = "Lesson 2 was missing" }), HttpStatusCode.Created);
        refund.GetProperty("status").GetString().Should().Be("issued");
        refund.GetProperty("needsSecondApprover").GetBoolean().Should().BeFalse();

        var wallet = await learner.GetFromJsonAsync<JsonElement>("/api/v1/wallet/me", Json);
        wallet.GetProperty("balance").GetInt64().Should().Be(9, "₦9,000 of a ₦45,000 / 45-unit sale");
        var last = wallet.GetProperty("recentTransactions")[0];
        last.GetProperty("kind").GetString().Should().Be("refund");
        last.GetProperty("units").GetInt64().Should().Be(9);
        last.GetProperty("value").GetProperty("amount").GetInt64().Should().Be(900_000);
        wallet.GetProperty("openLots").EnumerateArray().Should().ContainSingle(l => l.GetProperty("kind").GetString() == "refund" && l.GetProperty("unitsRemaining").GetInt64() == 9);

        // Entry 5 (units): Dr 2010 7,200 + Dr 4010 1,800; Cr 2050 (wallet) 9,000. Cash never moves.
        (await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId) - before2050).Should().Be(Money.NairaAmount(9_000));
        (await BalanceAsync(AccountCodes.TutorPayable, tutorUserId) - before2010).Should().Be(Money.NairaAmount(-7_200));
        (await BalanceAsync(AccountCodes.CourseCommission) - before4010).Should().Be(Money.NairaAmount(-1_800));
        (await BalanceAsync(AccountCodes.BankClientFunds) - before1020).Should().Be(Money.Zero());
    }

    [Fact]
    public async Task An_organisation_funds_its_programme_from_the_wallet_and_the_invoice_settles_with_entry_6_units()
    {
        var now = DateTime.UtcNow;
        var (tutor, _, slug, courseId) = await CatalogueTests.PublishedCourseAsync(factory);
        var offeringId = (await tutor.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/{slug}", Json)).GetProperty("offerings")[0].GetProperty("offeringId").GetGuid();
        var (owner, organisationId, _) = await SponsorshipAndApplicationsTests.VerifiedSponsorAsync(factory, "Units CSR " + Guid.NewGuid().ToString("N")[..6]);
        var programmes = $"/api/v1/sponsorship/organisations/{organisationId}/programmes";

        var programmeId = (await ReadAsync(await owner.PostAsJsonAsync(programmes, new { name = "Units-funded cohort", objectives = "Three sponsored graduates" }), HttpStatusCode.Created)).GetProperty("programmeId").GetGuid();
        await ReadAsync(await owner.PutAsJsonAsync($"{programmes}/{programmeId}", new { courseId, offeringId, seats = 3, sponsorSharePercent = 70, budget = new { amount = 500_000_000, currency = "NGN" } }), HttpStatusCode.OK);
        var startsAt = now.AddDays(3);
        await ReadAsync(await owner.PutAsJsonAsync($"{programmes}/{programmeId}", new { eligibilityRules = new { minAge = 21 }, applicationsCloseAt = now.AddDays(2), startsAt, endsAt = startsAt.AddDays(91), selectionMode = "sponsor" }), HttpStatusCode.OK);
        await ReadAsync(await owner.PostAsync($"{programmes}/{programmeId}/quote", null), HttpStatusCode.OK);

        // Nothing to fund before the quote is approved.
        Key(owner);
        (await ReadAsync(await owner.PostAsync($"{programmes}/{programmeId}/fund-from-wallet", null), HttpStatusCode.Conflict)).GetProperty("code").GetString().Should().Be("programme_not_invoiced");

        Key(owner);
        var invoice = await ReadAsync(await owner.PostAsJsonAsync($"{programmes}/{programmeId}/quote/approve", new { dueDays = 14 }), HttpStatusCode.OK);
        var totalKobo = invoice.GetProperty("total").GetProperty("amount").GetInt64();
        var subtotalKobo = invoice.GetProperty("subtotal").GetProperty("amount").GetInt64();
        var vatKobo = invoice.GetProperty("vat").GetProperty("amount").GetInt64();
        totalKobo.Should().Be(335_820_000);
        var invoiceId = invoice.GetProperty("invoiceId").GetGuid();

        // An empty organisation wallet cannot pay.
        Key(owner);
        (await ReadAsync(await owner.PostAsync($"{programmes}/{programmeId}/fund-from-wallet", null), HttpStatusCode.Conflict)).GetProperty("code").GetString().Should().Be("insufficient_units");

        var units = (totalKobo + DefaultRateKobo - 1) / DefaultRateKobo; // 3,359 units cover ₦3,358,200
        var (walletId, _, purchasedKobo) = await BuyUnitsAsync(owner, units, organisationId);
        var orgWallet = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/wallet/organisations/{organisationId}", Json);
        orgWallet.GetProperty("ownerType").GetString().Should().Be("organisation");
        orgWallet.GetProperty("balance").GetInt64().Should().Be(units);

        var before2050 = await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId);
        var before2020 = await BalanceAsync(AccountCodes.ProgrammeFundsHeld, programmeId);
        var before2040 = await BalanceAsync(AccountCodes.VatPayable);
        var before4060 = await BalanceAsync(AccountCodes.UnitRevaluation);

        Key(owner);
        var funded = await ReadAsync(await owner.PostAsync($"{programmes}/{programmeId}/fund-from-wallet", null), HttpStatusCode.OK);
        funded.GetProperty("invoiceId").GetGuid().Should().Be(invoiceId);
        funded.GetProperty("invoiceStatus").GetString().Should().Be("paid");
        funded.GetProperty("unitsRedeemed").GetInt64().Should().Be(units);
        funded.GetProperty("unitRateKobo").GetInt64().Should().Be(DefaultRateKobo);
        funded.GetProperty("amountApplied").GetProperty("amount").GetInt64().Should().Be(totalKobo);
        funded.GetProperty("outstanding").GetProperty("amount").GetInt64().Should().Be(0);
        funded.GetProperty("walletBalanceAfter").GetInt64().Should().Be(0);

        // Entry 6 (units): Dr 2050 the units' value (whole units, so slightly more than the invoice); Cr 2020 subtotal; Cr 2040 VAT; the surplus to 4060.
        (await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId) - before2050).Should().Be(Money.Kobo(-purchasedKobo));
        (await BalanceAsync(AccountCodes.ProgrammeFundsHeld, programmeId) - before2020).Should().Be(Money.Kobo(subtotalKobo));
        (await BalanceAsync(AccountCodes.VatPayable) - before2040).Should().Be(Money.Kobo(vatKobo));
        (await BalanceAsync(AccountCodes.UnitRevaluation) - before4060).Should().Be(Money.Kobo(purchasedKobo - totalKobo));

        await WaitUntilAsync(async () => (await owner.GetFromJsonAsync<JsonElement>($"{programmes}/{programmeId}", Json)).GetProperty("isFunded").GetBoolean(), TimeSpan.FromSeconds(60), "programme funding from invoice-paid");
        (await owner.GetFromJsonAsync<JsonElement>($"{programmes}/{programmeId}", Json)).GetProperty("fundedAmount").GetProperty("amount").GetInt64().Should().Be(totalKobo);

        // Paying twice is refused; the wallet shows the redemption.
        Key(owner);
        (await ReadAsync(await owner.PostAsync($"{programmes}/{programmeId}/fund-from-wallet", null), HttpStatusCode.Conflict)).GetProperty("code").GetString().Should().BeOneOf("invoice_already_paid", "programme_not_invoiced");
        var after = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/wallet/organisations/{organisationId}", Json);
        after.GetProperty("balance").GetInt64().Should().Be(0);
        after.GetProperty("recentTransactions")[0].GetProperty("kind").GetString().Should().Be("redemption");
        after.GetProperty("recentTransactions")[0].GetProperty("purpose").GetString().Should().Be("sponsorship");

        // A learner who is not a member cannot read the organisation's wallet.
        var (stranger, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Stranger");
        (await stranger.GetAsync($"/api/v1/wallet/organisations/{organisationId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unit_rate_changes_need_a_second_staff_member()
    {
        var staff = await factory.SignInAsStaffAsync();
        Key(staff);
        var proposed = await ReadAsync(await staff.PostAsJsonAsync("/api/v1/admin/wallet/rates", new { rateKobo = 150_000, effectiveFrom = DateTime.UtcNow, rationale = "Align with the new course price band" }), HttpStatusCode.Created);
        proposed.GetProperty("status").GetString().Should().Be("proposed");
        proposed.GetProperty("rateKobo").GetInt64().Should().Be(150_000);
        proposed.GetProperty("version").GetInt32().Should().BeGreaterThan(0);
        var rateId = proposed.GetProperty("rateId").GetGuid();

        Key(staff);
        var self = await ReadAsync(await staff.PostAsJsonAsync($"/api/v1/admin/wallet/rates/{rateId}/decision", new { decision = "approved" }), HttpStatusCode.Forbidden);
        self.GetProperty("code").GetString().Should().Be("maker_checker_violation");

        var list = await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/wallet/rates", Json);
        list.GetProperty("effectiveRateKobo").GetInt64().Should().Be(DefaultRateKobo);
        list.GetProperty("effectiveSource").GetString().Should().Be("default");
        list.GetProperty("rates").EnumerateArray().Should().Contain(r => r.GetProperty("rateId").GetGuid() == rateId && r.GetProperty("status").GetString() == "proposed");
        (await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/wallet/rate", Json)).GetProperty("rateKobo").GetInt64().Should().Be(DefaultRateKobo, "a proposal changes nothing");

        Key(staff);
        (await ReadAsync(await staff.PostAsJsonAsync($"/api/v1/admin/wallet/rates/{rateId}/decision", new { decision = "rejected" }), HttpStatusCode.UnprocessableEntity)).GetProperty("code").GetString().Should().Be("validation_failed", "a rejection needs a note");
        Key(staff);
        var rejected = await ReadAsync(await staff.PostAsJsonAsync($"/api/v1/admin/wallet/rates/{rateId}/decision", new { decision = "rejected", note = "Too steep for the first quarter" }), HttpStatusCode.OK);
        rejected.GetProperty("status").GetString().Should().Be("rejected");
        rejected.GetProperty("decisionNote").GetString().Should().Be("Too steep for the first quarter");

        // Out-of-range rates and past effective dates are refused.
        Key(staff);
        (await staff.PostAsJsonAsync("/api/v1/admin/wallet/rates", new { rateKobo = 10, effectiveFrom = DateTime.UtcNow, rationale = "x" })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Key(staff);
        (await ReadAsync(await staff.PostAsJsonAsync("/api/v1/admin/wallet/rates", new { rateKobo = 120_000, effectiveFrom = DateTime.UtcNow.AddDays(-1), rationale = "Backdated" }), HttpStatusCode.UnprocessableEntity)).GetProperty("code").GetString().Should().Be("effective_from_past");
        (await factory.CreateClient().PostAsJsonAsync("/api/v1/admin/wallet/rates", new { rateKobo = 150_000, effectiveFrom = DateTime.UtcNow, rationale = "anon" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Staff_adjust_wallets_with_a_reason_and_no_cash()
    {
        var (learner, signIn) = await factory.SignInWithPhoneAsync(NewPhone(), "Goodwill learner");
        var staff = await factory.SignInAsStaffAsync();

        Key(staff);
        var credited = await ReadAsync(await staff.PostAsJsonAsync($"/api/v1/admin/wallet/user/{signIn.UserId}/adjustments", new { units = 5, reason = "Goodwill after a cancelled class" }), HttpStatusCode.OK);
        credited.GetProperty("units").GetInt64().Should().Be(5);
        credited.GetProperty("balanceAfter").GetInt64().Should().Be(5);
        var walletId = credited.GetProperty("walletId").GetGuid();
        var before2050 = await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId);

        var mine = await learner.GetFromJsonAsync<JsonElement>("/api/v1/wallet/me", Json);
        mine.GetProperty("balance").GetInt64().Should().Be(5);
        var tx = mine.GetProperty("recentTransactions")[0];
        tx.GetProperty("kind").GetString().Should().Be("adjustment");
        tx.GetProperty("value").GetProperty("amount").GetInt64().Should().Be(0);
        tx.GetProperty("description").GetString().Should().Be("Goodwill after a cancelled class");

        var lookup = await staff.GetFromJsonAsync<JsonElement>($"/api/v1/admin/wallet/user/{signIn.UserId}", Json);
        lookup.GetProperty("walletId").GetGuid().Should().Be(walletId);
        lookup.GetProperty("balance").GetInt64().Should().Be(5);
        lookup.GetProperty("openLots").EnumerateArray().Should().ContainSingle(l => l.GetProperty("kind").GetString() == "adjustment" && l.GetProperty("valueRemaining").GetProperty("amount").GetInt64() == 0);

        Key(staff);
        var debited = await ReadAsync(await staff.PostAsJsonAsync($"/api/v1/admin/wallet/user/{signIn.UserId}/adjustments", new { units = -3, reason = "Correction: duplicate goodwill" }), HttpStatusCode.OK);
        debited.GetProperty("balanceAfter").GetInt64().Should().Be(2);
        (await learner.GetFromJsonAsync<JsonElement>("/api/v1/wallet/me", Json)).GetProperty("balance").GetInt64().Should().Be(2);

        Key(staff);
        (await ReadAsync(await staff.PostAsJsonAsync($"/api/v1/admin/wallet/user/{signIn.UserId}/adjustments", new { units = -1000, reason = "Too much" }), HttpStatusCode.Conflict)).GetProperty("code").GetString().Should().Be("insufficient_units");
        Key(staff);
        (await staff.PostAsJsonAsync($"/api/v1/admin/wallet/user/{signIn.UserId}/adjustments", new { units = 0, reason = "Nothing" })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Key(staff);
        (await staff.PostAsJsonAsync($"/api/v1/admin/wallet/user/{signIn.UserId}/adjustments", new { units = 1, reason = "" })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await staff.GetAsync($"/api/v1/admin/wallet/team/{signIn.UserId}")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "ownerType must be user or organisation");

        // Adjustments carry no cash: the unit liability is untouched and learners cannot reach the staff routes.
        (await BalanceAsync(AccountCodes.UnitBalancesHeld, walletId) - before2050).Should().Be(Money.Zero());
        (await learner.GetAsync($"/api/v1/admin/wallet/user/{signIn.UserId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
