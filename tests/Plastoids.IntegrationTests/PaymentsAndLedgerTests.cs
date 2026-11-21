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
using Plastoids.Modules.Ledger.Persistence;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

[Collection(ApiCollection.Name)]
public class PaymentsAndLedgerTests(PlastoidsApiFactory factory)
{
    private async Task<Money> BalanceAsync(string code, Guid? owner = null)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILedgerService>().GetBalanceAsync(code, owner, CancellationToken.None);
    }

    private static HttpRequestMessage SignedWebhook(string payload, string signature) =>
        new(HttpMethod.Post, "/api/v1/payments/webhooks/fake") { Content = new StringContent(payload, Encoding.UTF8, "application/json"), Headers = { { "x-paystack-signature", signature } } };

    [Fact]
    public async Task Course_purchase_runs_ledger_entries_1_2_3_and_5_with_webhook_replay_and_idempotency()
    {
        // A published course from a verified tutor, then a learner with a 30-minute quote.
        var (tutor, tutorUserId, slug, _) = await CatalogueTests.PublishedCourseAsync(factory);
        var (learner, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Buyer");
        var detail = await learner.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/{slug}", Json);
        var quoteId = detail.GetProperty("offerings")[0].GetProperty("quoteId").GetString()!;

        var before2010 = await BalanceAsync(AccountCodes.TutorPayable, tutorUserId);
        var before4010 = await BalanceAsync(AccountCodes.CourseCommission);
        var before1030 = await BalanceAsync(AccountCodes.GatewayReceivable);

        // Create intent (Idempotency-Key): replay returns the same intent; a different body under the same key is refused.
        var key = Guid.NewGuid().ToString("N");
        learner.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var created = await learner.PostAsJsonAsync("/api/v1/payments/intents", new { purpose = "course", quoteId });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var intent = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        var intentId = intent.GetProperty("paymentIntentId").GetGuid();
        var reference = intent.GetProperty("gatewayReference").GetString()!;
        intent.GetProperty("amount").GetProperty("amount").GetInt64().Should().Be(4_500_000);
        intent.GetProperty("commissionBps").GetInt32().Should().Be(2000);
        var replay = await learner.PostAsJsonAsync("/api/v1/payments/intents", new { purpose = "course", quoteId });
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        (await replay.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("paymentIntentId").GetGuid().Should().Be(intentId);
        var conflict = await learner.PostAsJsonAsync("/api/v1/payments/intents", new { purpose = "co_pay", referenceId = Guid.NewGuid(), amount = new { amount = 100000, currency = "NGN" } });
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        learner.DefaultRequestHeaders.Remove("Idempotency-Key");

        // Gateway webhook (signed) confirms the charge → entry 1. Replaying the same event is a no-op.
        var gateway = factory.Services.GetRequiredService<FakePaymentGateway>();
        var anonymous = factory.CreateClient();
        var (payload, signature) = gateway.BuildWebhook("charge.success", reference, Money.NairaAmount(45_000), DateTime.UtcNow);
        (await anonymous.SendAsync(SignedWebhook(payload, "bad"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var first = await anonymous.SendAsync(SignedWebhook(payload, signature));
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        (await first.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("outcome").GetString().Should().Be("confirmed");
        var second = await anonymous.SendAsync(SignedWebhook(payload, signature));
        (await second.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("outcome").GetString().Should().Be("duplicate");

        (await BalanceAsync(AccountCodes.TutorPayable, tutorUserId) - before2010).Should().Be(Money.NairaAmount(36_000));
        (await BalanceAsync(AccountCodes.CourseCommission) - before4010).Should().Be(Money.NairaAmount(9_000));
        (await BalanceAsync(AccountCodes.GatewayReceivable) - before1030).Should().Be(Money.NairaAmount(45_000));

        var view = await learner.GetFromJsonAsync<JsonElement>($"/api/v1/payments/intents/{intentId}", Json);
        view.GetProperty("status").GetString().Should().Be("succeeded");
        var receipt = await learner.GetFromJsonAsync<JsonElement>($"/api/v1/payments/intents/{intentId}/receipt", Json);
        receipt.GetProperty("receiptNumber").GetString().Should().StartWith("RCP-");
        var mine = await learner.GetFromJsonAsync<JsonElement>("/api/v1/payments/me", Json);
        mine.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("paymentIntentId").GetGuid()).Should().Contain(intentId);

        // Entry 2: settlement import (fake report settles everything with the charge-time fee).
        var staff = await factory.SignInAsStaffAsync();
        var before1020 = await BalanceAsync(AccountCodes.BankClientFunds);
        var before5010 = await BalanceAsync(AccountCodes.ProcessingFees);
        var settled = await staff.PostAsync("/api/v1/admin/payments/settlements/import", null);
        settled.StatusCode.Should().Be(HttpStatusCode.OK, await settled.Content.ReadAsStringAsync());
        // The import settles every unsettled charge (other tests' payments included), so the ledger moves by the report totals; this charge contributes 45,000 gross / 775 fee.
        var imported = await settled.Content.ReadFromJsonAsync<JsonElement>(Json);
        imported.GetProperty("settled").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        var gross = imported.GetProperty("grossKobo").GetInt64();
        var fees = imported.GetProperty("feesKobo").GetInt64();
        gross.Should().BeGreaterThanOrEqualTo(4_500_000);
        fees.Should().BeGreaterThanOrEqualTo(77_500);
        (await BalanceAsync(AccountCodes.BankClientFunds) - before1020).Should().Be(Money.Kobo(gross - fees));
        (await BalanceAsync(AccountCodes.ProcessingFees) - before5010).Should().Be(Money.Kobo(fees));
        (await BalanceAsync(AccountCodes.GatewayReceivable)).Should().Be(before1030 + Money.NairaAmount(45_000) - Money.Kobo(gross), "the receivable was cleared by settlement");
        (await staff.PostAsync("/api/v1/admin/payments/settlements/import", null)).IsSuccessStatusCode.Should().BeTrue("re-import is idempotent");
        (await BalanceAsync(AccountCodes.BankClientFunds) - before1020).Should().Be(Money.Kobo(gross - fees));

        // Entry 3: sweep (hold is 0 days in tests).
        var before1010 = await BalanceAsync(AccountCodes.BankOperating);
        var swept = await staff.PostAsync("/api/v1/admin/payments/sweeps/run", null);
        swept.StatusCode.Should().Be(HttpStatusCode.OK, await swept.Content.ReadAsStringAsync());
        // The sweep moves every settled-but-unswept commission (other tests' payments included): assert the ledger moved by the sweep total, which includes this sale's 8,225.
        var sweptBody = await swept.Content.ReadFromJsonAsync<JsonElement>(Json);
        var sweptAmount = sweptBody.GetProperty("amount").GetProperty("amount").GetInt64();
        sweptAmount.Should().BeGreaterThanOrEqualTo(822_500);
        (await BalanceAsync(AccountCodes.BankOperating) - before1010).Should().Be(Money.Kobo(sweptAmount));

        // Entry 5: a ₦4,500 refund issues immediately (below ₦100,000) and reverses both shares pro rata.
        staff.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var refund = await staff.PostAsJsonAsync($"/api/v1/admin/payments/intents/{intentId}/refunds", new { amount = new { amount = 450000, currency = "NGN" }, reason = "Lesson 1 was missing" });
        refund.StatusCode.Should().Be(HttpStatusCode.Created, await refund.Content.ReadAsStringAsync());
        (await refund.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetString().Should().Be("issued");
        (await BalanceAsync(AccountCodes.TutorPayable, tutorUserId) - before2010).Should().Be(Money.NairaAmount(36_000 - 3_600));
        (await BalanceAsync(AccountCodes.CourseCommission) - before4010).Should().Be(Money.NairaAmount(9_000 - 900));

        // Over the four-eyes threshold (₦100,000) the maker cannot approve their own refund: a ₦150,000 co-pay.
        learner.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var coPay = await learner.PostAsJsonAsync("/api/v1/payments/intents", new { purpose = "co_pay", referenceId = Guid.CreateVersion7(), amount = new { amount = 15_000_000, currency = "NGN" } });
        coPay.StatusCode.Should().Be(HttpStatusCode.Created, await coPay.Content.ReadAsStringAsync());
        var coPayBody = await coPay.Content.ReadFromJsonAsync<JsonElement>(Json);
        var coPayId = coPayBody.GetProperty("paymentIntentId").GetGuid();
        var (cpPayload, cpSignature) = gateway.BuildWebhook("charge.success", coPayBody.GetProperty("gatewayReference").GetString()!, Money.NairaAmount(150_000), DateTime.UtcNow);
        (await anonymous.SendAsync(SignedWebhook(cpPayload, cpSignature))).StatusCode.Should().Be(HttpStatusCode.OK);
        learner.DefaultRequestHeaders.Remove("Idempotency-Key");

        staff.DefaultRequestHeaders.Remove("Idempotency-Key");
        staff.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var big = await staff.PostAsJsonAsync($"/api/v1/admin/payments/intents/{coPayId}/refunds", new { amount = new { amount = 15_000_000, currency = "NGN" }, reason = "Programme cancelled before start" });
        big.StatusCode.Should().Be(HttpStatusCode.Created, await big.Content.ReadAsStringAsync());
        var bigBody = await big.Content.ReadFromJsonAsync<JsonElement>(Json);
        bigBody.GetProperty("status").GetString().Should().Be("pendingapproval");
        staff.DefaultRequestHeaders.Remove("Idempotency-Key");
        staff.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var selfApprove = await staff.PostAsync($"/api/v1/admin/payments/refunds/{bigBody.GetProperty("refundId").GetGuid()}/approve", null);
        selfApprove.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await selfApprove.Content.ReadAsStringAsync()).Should().Contain("four_eyes_required");

        // payments.payment-succeeded.v1 and refund-issued.v1 went to the outbox.
        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            (await db.Outbox.AsNoTracking().CountAsync(m => m.PartitionKey == intentId.ToString("D") && m.Topic == "plastoids.payments.payment-succeeded.v1")).Should().Be(1);
            (await db.Outbox.AsNoTracking().CountAsync(m => m.Topic == "plastoids.payments.refund-issued.v1")).Should().BeGreaterThanOrEqualTo(1);
        }

        tutor.Dispose();
    }

    [Fact]
    public async Task Sponsor_invoice_is_matched_from_a_virtual_account_credit_entry_6_with_partial_payments_flagged()
    {
        var (owner, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Sponsor finance");
        var org = await owner.PostAsJsonAsync("/api/v1/identity/organisations", new { name = "Access CSR", type = "sponsor", cacNumber = "RC" + Random.Shared.Next(100000, 999999), signatoryName = "Mr Ojo", signatoryEmail = "csr@access.example" });
        var organisationId = (await org.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("organisationId").GetGuid();
        var programmeId = Guid.CreateVersion7();

        owner.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var invoice = await owner.PostAsJsonAsync($"/api/v1/payments/organisations/{organisationId}/invoices", new
        {
            programmeId,
            customerName = "Access CSR",
            lines = new object[]
            {
                new { kind = "tuition", description = "Tuition 500 seats", quantity = 500, unitAmount = new { amount = 15_000_000, currency = "NGN" } },
                new { kind = "management_fee", description = "Programme management fee", quantity = 1, unitAmount = new { amount = 1_500_000_000, currency = "NGN" } },
            },
            dueDays = 14,
        });
        invoice.StatusCode.Should().Be(HttpStatusCode.Created, await invoice.Content.ReadAsStringAsync());
        var inv = await invoice.Content.ReadFromJsonAsync<JsonElement>(Json);
        inv.GetProperty("vat").GetProperty("amount").GetInt64().Should().Be(112_500_000);
        inv.GetProperty("total").GetProperty("amount").GetInt64().Should().Be(9_112_500_000);
        var account = inv.GetProperty("virtualAccountNumber").GetString()!;
        var invoiceId = inv.GetProperty("invoiceId").GetGuid();

        var before1020 = await BalanceAsync(AccountCodes.BankClientFunds);
        var before2040 = await BalanceAsync(AccountCodes.VatPayable);

        var gateway = factory.Services.GetRequiredService<FakePaymentGateway>();
        var anonymous = factory.CreateClient();
        var (payload, signature) = gateway.BuildWebhook("virtual_account.credit", "va_" + Guid.NewGuid().ToString("N")[..10], Money.NairaAmount(91_125_000), DateTime.UtcNow, account);
        var credit = await anonymous.SendAsync(SignedWebhook(payload, signature));
        credit.StatusCode.Should().Be(HttpStatusCode.OK, await credit.Content.ReadAsStringAsync());
        (await credit.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("outcome").GetString().Should().Be("matched");

        (await BalanceAsync(AccountCodes.BankClientFunds) - before1020).Should().Be(Money.NairaAmount(91_125_000));
        (await BalanceAsync(AccountCodes.ProgrammeFundsHeld, programmeId)).Should().Be(Money.NairaAmount(90_000_000));
        (await BalanceAsync(AccountCodes.VatPayable) - before2040).Should().Be(Money.NairaAmount(1_125_000));

        var paid = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/payments/organisations/{organisationId}/invoices/{invoiceId}", Json);
        paid.GetProperty("status").GetString().Should().Be("paid");

        // Second invoice: a partial transfer is flagged, an overpayment is held.
        owner.DefaultRequestHeaders.Remove("Idempotency-Key");
        owner.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var small = await owner.PostAsJsonAsync($"/api/v1/payments/organisations/{organisationId}/invoices", new { customerName = "Access CSR", lines = new object[] { new { kind = "seat_fee", description = "Seat fees", quantity = 10, unitAmount = new { amount = 1_200_000, currency = "NGN" } } } });
        var smallInv = await small.Content.ReadFromJsonAsync<JsonElement>(Json);
        var smallAccount = smallInv.GetProperty("virtualAccountNumber").GetString()!;
        var (p1, s1) = gateway.BuildWebhook("virtual_account.credit", "va_" + Guid.NewGuid().ToString("N")[..10], Money.NairaAmount(50_000), DateTime.UtcNow, smallAccount);
        (await (await anonymous.SendAsync(SignedWebhook(p1, s1))).Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("outcome").GetString().Should().Be("partial");
        var (p2, s2) = gateway.BuildWebhook("virtual_account.credit", "va_" + Guid.NewGuid().ToString("N")[..10], Money.NairaAmount(500_000), DateTime.UtcNow, smallAccount);
        (await (await anonymous.SendAsync(SignedWebhook(p2, s2))).Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("outcome").GetString().Should().Be("held");

        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            (await db.Outbox.AsNoTracking().CountAsync(m => m.Topic == "plastoids.payments.invoice-paid.v1" && m.PartitionKey == invoiceId.ToString("D"))).Should().Be(1);
        }
    }

    [Fact]
    public async Task Ledger_is_append_only_rejects_unbalanced_drafts_and_reconciliation_freezes_payouts_on_shortfall()
    {
        var staff = await factory.SignInAsStaffAsync();
        Guid entryId;
        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var ledger = scope.ServiceProvider.GetRequiredService<ILedgerService>();
            var unbalanced = await ledger.PostAsync(new JournalEntryDraft("test:unbalanced:" + Guid.NewGuid().ToString("N"), "test", "bad", [PostingDraft.Dr(AccountCodes.BankOperating, Money.NairaAmount(10)), PostingDraft.Cr(AccountCodes.BankClientFunds, Money.NairaAmount(9))]), CancellationToken.None);
            unbalanced.Error.Code.Should().Be("ledger_unbalanced");

            // Entry 4 (tutor paid after the refund window) posted directly, as Payouts will in Sprint 7.
            var tutor = Guid.CreateVersion7();
            var reference = "test:payout:" + Guid.NewGuid().ToString("N");
            var posted = await ledger.PostAsync(new JournalEntryDraft(reference, "tutor_payout", "Weekly payout",
                [PostingDraft.Dr(AccountCodes.TutorPayable, Money.NairaAmount(36_000), SubLedgerOwnerTypes.Tutor, tutor), PostingDraft.Cr(AccountCodes.BankClientFunds, Money.NairaAmount(36_000))]), CancellationToken.None);
            posted.IsSuccess.Should().BeTrue(posted.IsFailure ? posted.Error.Message : "");
            entryId = posted.Value;
            (await ledger.PostAsync(new JournalEntryDraft(reference, "tutor_payout", "again", [PostingDraft.Dr(AccountCodes.TutorPayable, Money.NairaAmount(1)), PostingDraft.Cr(AccountCodes.BankClientFunds, Money.NairaAmount(1))]), CancellationToken.None)).Value.Should().Be(entryId, "same reference → same entry");
            (await ledger.GetBalanceAsync(AccountCodes.TutorPayable, tutor, CancellationToken.None)).Should().Be(Money.NairaAmount(-36_000));
        }

        await using (var connection = new MySqlConnection(Plastoids.Infrastructure.InfrastructureServiceCollectionExtensions.EnsureGuidFormat(factory.MySqlConnectionString)))
        {
            await connection.OpenAsync();
            await using var update = connection.CreateCommand();
            update.CommandText = "UPDATE ledger_postings SET debit_kobo = 1 WHERE journal_entry_id = @id";
            update.Parameters.AddWithValue("@id", entryId);
            var failure = await Record.ExceptionAsync(() => update.ExecuteNonQueryAsync());
            failure.Should().BeOfType<MySqlException>().Which.Message.Should().Contain("append-only");
        }

        var entries = await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/ledger/entries?kind=tutor_payout&limit=5", Json);
        entries.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).Should().Contain(entryId);
        var balance = await staff.GetFromJsonAsync<JsonElement>($"/api/v1/admin/ledger/accounts/{AccountCodes.BankClientFunds}/balance", Json);
        balance.GetProperty("balance").GetProperty("currency").GetString().Should().Be("NGN");

        // Reconciliation: with the fake bank mirroring the ledger there is no shortfall; an understated statement freezes payouts.
        var bank = factory.Services.GetRequiredService<FakeBankStatement>();
        bank.BalanceOverride = null;
        var ok = await staff.PostAsync("/api/v1/admin/ledger/reconciliation/run", null);
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        bank.BalanceOverride = Money.NairaAmount(-1_000_000_000);
        var shortfall = await (await staff.PostAsync("/api/v1/admin/ledger/reconciliation/run", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        shortfall.GetProperty("shortfall").GetBoolean().Should().BeTrue();
        bank.BalanceOverride = null;
        var status = await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/ledger/reconciliation", Json);
        status.GetProperty("payoutsFrozen").GetBoolean().Should().BeTrue();

        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
            (await db.Outbox.AsNoTracking().CountAsync(m => m.Topic == "plastoids.ledger.shortfall-detected.v1")).Should().BeGreaterThanOrEqualTo(1);
            (await scope.ServiceProvider.GetRequiredService<IPayoutControls>().ArePayoutsFrozenAsync(CancellationToken.None)).Should().BeTrue();
        }

        // Finance lifts the freeze (Sprint 7 endpoint) so later tests can run payouts.
        staff.DefaultRequestHeaders.Remove("Idempotency-Key");
        staff.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var unfrozen = await staff.PostAsJsonAsync("/api/v1/admin/ledger/reconciliation/unfreeze", new { reason = "Simulated shortfall in the integration suite" });
        unfrozen.StatusCode.Should().Be(HttpStatusCode.OK, await unfrozen.Content.ReadAsStringAsync());
        (await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/ledger/reconciliation", Json)).GetProperty("payoutsFrozen").GetBoolean().Should().BeFalse();
    }
}
