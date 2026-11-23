using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Ledger.Jobs;
using Plastoids.Modules.Payouts.Jobs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

[Collection(ApiCollection.Name)]
public class PayoutsTests(PlastoidsApiFactory factory)
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

    private static HttpRequestMessage PayoutWebhook(string payload, string signature) =>
        new(HttpMethod.Post, "/api/v1/payouts/webhooks/fake") { Content = new StringContent(payload, Encoding.UTF8, "application/json"), Headers = { { "x-payout-signature", signature } } };

    [Fact]
    public async Task Weekly_run_pays_a_tutor_with_maker_checker_entry_4_reversal_and_zero_reconciliation_breaks()
    {
        var (tutor, tutorUserId, slug, _) = await CatalogueTests.PublishedCourseAsync(factory);
        var (learner, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Buyer");
        await LearningAndCertificatesTests.EnrolByPaymentAsync(factory, learner, slug);

        // The sale (entry 1 credited 2010 with ₦36,000) becomes a held earning; the hold is 0 days in tests.
        await WaitUntilAsync(async () => (await tutor.GetFromJsonAsync<JsonElement>("/api/v1/payouts/me", Json)).GetProperty("gross").GetProperty("amount").GetInt64() >= 4_500_000, TimeSpan.FromSeconds(60), "earning from payment-succeeded");
        var earnings = await tutor.GetFromJsonAsync<JsonElement>("/api/v1/payouts/me", Json);
        earnings.GetProperty("commission").GetProperty("amount").GetInt64().Should().Be(900_000);
        earnings.GetProperty("held").GetProperty("amount").GetInt64().Should().Be(3_600_000);
        earnings.GetProperty("available").GetProperty("amount").GetInt64().Should().Be(0);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<EarningsReleaseJob>().RunAsync(DateTime.UtcNow.AddMinutes(1), CancellationToken.None)).Released.Should().BeGreaterThanOrEqualTo(1);
        }

        (await tutor.GetFromJsonAsync<JsonElement>("/api/v1/payouts/me", Json)).GetProperty("available").GetProperty("amount").GetInt64().Should().Be(3_600_000);

        // No payout account yet → skipped; after a name-matched account → eligible.
        var staff = await factory.SignInAsStaffAsync();
        var queue = await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/payouts/queue", Json);
        var row = queue.GetProperty("tutors").EnumerateArray().Single(t => t.TryGetProperty("tutorUserId", out var tu) && tu.ValueKind == JsonValueKind.String && tu.GetGuid() == tutorUserId);
        row.GetProperty("skipReason").GetString().Should().Be("no_payout_account");
        var resolver = factory.Services.GetRequiredService<FakeBankAccountResolver>();
        var accountNumber = "01" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        resolver.KnownAccounts[accountNumber] = "COURSE AUTHOR";
        (await tutor.PutAsJsonAsync("/api/v1/tutors/me/payout-account", new { bankCode = "058", accountNumber })).StatusCode.Should().Be(HttpStatusCode.OK);

        // Dispute hold blocks the run (max 5 working days), then is released.
        Key(staff);
        var hold = await staff.PostAsJsonAsync("/api/v1/admin/payouts/holds", new { tutorUserId, reason = "Learner dispute on the course refund" });
        hold.StatusCode.Should().Be(HttpStatusCode.Created, await hold.Content.ReadAsStringAsync());
        var holdId = (await hold.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("holdId").GetGuid();
        Key(staff);
        var blocked = await staff.PostAsync("/api/v1/admin/payouts/batches", null);
        var blockedBody = await blocked.Content.ReadAsStringAsync();
        blockedBody.Should().Contain("dispute_open", "the held tutor is skipped whether or not other payees (from other tests) make a batch");
        if (blocked.StatusCode == HttpStatusCode.Created)
        {
            // Other tests left payable payees with accounts: a batch was drafted without this tutor. Reject it so it does not hold their amounts.
            var draftedWhileHeld = JsonSerializer.Deserialize<JsonElement>(blockedBody, Json);
            draftedWhileHeld.GetProperty("payouts").EnumerateArray().Any(p => p.TryGetProperty("tutorUserId", out var tu) && tu.ValueKind == JsonValueKind.String && tu.GetGuid() == tutorUserId).Should().BeFalse();
            Key(staff);
            (await staff.PostAsJsonAsync($"/api/v1/admin/payouts/batches/{draftedWhileHeld.GetProperty("batchId").GetGuid()}/reject", new { reason = "Drafted while a hold was open; test clean-up" })).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        else
        {
            blocked.StatusCode.Should().Be(HttpStatusCode.Conflict, blockedBody);
        }
        Key(staff);
        (await staff.PostAsJsonAsync($"/api/v1/admin/payouts/holds/{holdId}/release", new { resolution = "Dispute dismissed" })).StatusCode.Should().Be(HttpStatusCode.OK);

        // Maker–checker: the staff member who drafts the batch cannot approve it.
        Key(staff);
        var drafted = await staff.PostAsync("/api/v1/admin/payouts/batches", null);
        drafted.StatusCode.Should().Be(HttpStatusCode.Created, await drafted.Content.ReadAsStringAsync());
        var draft = await drafted.Content.ReadFromJsonAsync<JsonElement>(Json);
        draft.GetProperty("payouts").EnumerateArray().Single(p => p.TryGetProperty("tutorUserId", out var tu) && tu.ValueKind == JsonValueKind.String && tu.GetGuid() == tutorUserId).GetProperty("amount").GetProperty("amount").GetInt64().Should().Be(3_600_000);
        var draftId = draft.GetProperty("batchId").GetGuid();
        Key(staff);
        var selfApprove = await staff.PostAsync($"/api/v1/admin/payouts/batches/{draftId}/approve", null);
        selfApprove.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await selfApprove.Content.ReadAsStringAsync()).Should().Contain("maker_checker_violation");
        Key(staff);
        (await staff.PostAsJsonAsync($"/api/v1/admin/payouts/batches/{draftId}/reject", new { reason = "Drafted by hand; the weekly run will pick it up" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await tutor.GetFromJsonAsync<JsonElement>("/api/v1/payouts/me", Json)).GetProperty("available").GetProperty("amount").GetInt64().Should().Be(3_600_000, "a rejected batch releases the pending amount");

        // Weekly run: the system drafts, a human approves, the batch is submitted and the bank settles it (entry 4).
        Guid batchId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            batchId = (await scope.ServiceProvider.GetRequiredService<WeeklyPayoutRunJob>().RunAsync(DateTime.UtcNow, CancellationToken.None)) ?? Guid.Empty;
        }

        batchId.Should().NotBeEmpty();
        Key(staff);
        var approved = await staff.PostAsync($"/api/v1/admin/payouts/batches/{batchId}/approve", null);
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        Key(staff);
        var submitted = await staff.PostAsync($"/api/v1/admin/payouts/batches/{batchId}/submit", null);
        submitted.StatusCode.Should().Be(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
        var batch = await staff.GetFromJsonAsync<JsonElement>($"/api/v1/admin/payouts/batches/{batchId}", Json);
        var payout = batch.GetProperty("payouts").EnumerateArray().Single(p => p.TryGetProperty("tutorUserId", out var tu) && tu.ValueKind == JsonValueKind.String && tu.GetGuid() == tutorUserId);
        payout.GetProperty("status").GetString().Should().Be("submitted");
        var reference = payout.GetProperty("providerReference").GetString()!;

        var before1020 = await BalanceAsync(AccountCodes.BankClientFunds);
        var provider = factory.Services.GetRequiredService<FakePayoutProvider>();
        var (payload, signature) = provider.BuildWebhook("transfer.success", reference, Money.NairaAmount(36_000), DateTime.UtcNow, "NIP-TEST-1");
        (await factory.CreateClient().SendAsync(PayoutWebhook(payload, "bad"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var settled = await factory.CreateClient().SendAsync(PayoutWebhook(payload, signature));
        settled.StatusCode.Should().Be(HttpStatusCode.OK, await settled.Content.ReadAsStringAsync());
        (await settled.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("outcome").GetString().Should().Be("settled");
        (await (await factory.CreateClient().SendAsync(PayoutWebhook(payload, signature))).Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("outcome").GetString().Should().Be("duplicate");
        (await BalanceAsync(AccountCodes.TutorPayable, tutorUserId)).Should().Be(Money.Zero());
        (before1020 - await BalanceAsync(AccountCodes.BankClientFunds)).Should().Be(Money.NairaAmount(36_000));
        var afterPayout = await tutor.GetFromJsonAsync<JsonElement>("/api/v1/payouts/me", Json);
        afterPayout.GetProperty("paid").GetProperty("amount").GetInt64().Should().Be(3_600_000);
        afterPayout.GetProperty("available").GetProperty("amount").GetInt64().Should().Be(0);
        // The batch may carry other tests' payees: settle their transfers too so the batch can complete.
        foreach (var other in batch.GetProperty("payouts").EnumerateArray().Where(p => p.GetProperty("providerReference").GetString() != reference))
        {
            var (otherPayload, otherSignature) = provider.BuildWebhook("transfer.success", other.GetProperty("providerReference").GetString()!, Money.Kobo(other.GetProperty("amount").GetProperty("amount").GetInt64()), DateTime.UtcNow, "NIP-TEST-" + other.GetProperty("payoutId").GetGuid().ToString("N")[..6]);
            (await factory.CreateClient().SendAsync(PayoutWebhook(otherPayload, otherSignature))).IsSuccessStatusCode.Should().BeTrue();
        }

        (await staff.GetFromJsonAsync<JsonElement>($"/api/v1/admin/payouts/batches/{batchId}", Json)).GetProperty("status").GetString().Should().Be("completed");

        // Zero reconciliation breaks after the full weekly run.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var (assets, liabilities) = await scope.ServiceProvider.GetRequiredService<ReconciliationJob>().RunAsync(CancellationToken.None);
            assets.Should().BeGreaterThanOrEqualTo(liabilities);
        }

        (await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/payouts/queue", Json)).GetProperty("payoutsFrozen").GetBoolean().Should().BeFalse();

        // The bank reverses the transfer: funds return to the tutor's balance and the ledger.
        var (reversalPayload, reversalSignature) = provider.BuildWebhook("transfer.reversed", reference, Money.NairaAmount(36_000), DateTime.UtcNow, "NIP-TEST-1", "Account closed");
        (await (await factory.CreateClient().SendAsync(PayoutWebhook(reversalPayload, reversalSignature))).Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("outcome").GetString().Should().Be("reversed");
        (await BalanceAsync(AccountCodes.TutorPayable, tutorUserId)).Should().Be(Money.NairaAmount(36_000));
        (await tutor.GetFromJsonAsync<JsonElement>("/api/v1/payouts/me", Json)).GetProperty("available").GetProperty("amount").GetInt64().Should().Be(3_600_000, "a reversed payout restores the balance");

        var statement = await tutor.GetStringAsync("/api/v1/payouts/me/statement");
        statement.Should().StartWith("date,type,source,status").And.Contain(",earning,course,").And.Contain(",payout,bank,reversed,");

        // Frozen payouts block batch creation until finance lifts the freeze.
        var bank = factory.Services.GetRequiredService<FakeBankStatement>();
        var gatewayReport = factory.Services.GetRequiredService<FakeGatewaySettlementReport>();
        bank.BalanceOverride = Money.Zero();
        gatewayReport.InTransitOverride = Money.Zero();
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var (assets, liabilities) = await scope.ServiceProvider.GetRequiredService<ReconciliationJob>().RunAsync(CancellationToken.None);
            assets.Should().BeLessThan(liabilities, "the bank reports nothing while tutors are owed money");
        }
        finally
        {
            bank.BalanceOverride = null;
            gatewayReport.InTransitOverride = null;
        }

        Key(staff);
        var frozen = await staff.PostAsync("/api/v1/admin/payouts/batches", null);
        frozen.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await frozen.Content.ReadAsStringAsync()).Should().Contain("payouts_frozen");
        Key(staff);
        var unfrozen = await staff.PostAsJsonAsync("/api/v1/admin/ledger/reconciliation/unfreeze", new { reason = "Bank statement feed was stale; balance confirmed by finance" });
        unfrozen.StatusCode.Should().Be(HttpStatusCode.OK, await unfrozen.Content.ReadAsStringAsync());
        (await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/payouts/queue", Json)).GetProperty("payoutsFrozen").GetBoolean().Should().BeFalse();
        tutor.Dispose();
    }
}
