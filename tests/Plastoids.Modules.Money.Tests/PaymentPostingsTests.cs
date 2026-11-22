using Plastoids.Infrastructure.Integrations;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Ledger.Services;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Services;
using Plastoids.SharedKernel.Tenancy;
using M = Plastoids.SharedKernel.Domain.Money;

namespace Plastoids.Modules.Money.Tests;

/// <summary>Blueprint §18 illustrative entries 1, 2, 3, 5 and 6 reproduced from the posting builders.</summary>
public class PaymentPostingsTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Tutor = Guid.CreateVersion7();

    private static PaymentIntent CourseSale()
    {
        var intent = PaymentIntent.Create(Tenants.Marketplace, Guid.NewGuid(), PaymentPurposes.Course, Guid.NewGuid(), "q_1", "Course", M.NairaAmount(45_000), Tutor, 2000, "course_platform");
        intent.AttachCheckout("fake", "fk_ref", "https://checkout");
        return intent;
    }

    private static long Dr(JournalEntryDraft d, string code) => d.Postings.Where(p => p.AccountCode == code).Sum(p => p.Debit.AmountKobo);
    private static long Cr(JournalEntryDraft d, string code) => d.Postings.Where(p => p.AccountCode == code).Sum(p => p.Credit.AmountKobo);

    [Fact]
    public void Entry1_course_sale_splits_tutor_payable_and_commission()
    {
        var intent = CourseSale();
        intent.Commission.Should().Be(M.NairaAmount(9_000));
        intent.PayeeNet.Should().Be(M.NairaAmount(36_000));
        var d = PaymentPostings.Entry1Charge(intent, Now);
        LedgerService.Validate(d).IsSuccess.Should().BeTrue();
        Dr(d, AccountCodes.GatewayReceivable).Should().Be(4_500_000);
        Cr(d, AccountCodes.TutorPayable).Should().Be(3_600_000);
        Cr(d, AccountCodes.CourseCommission).Should().Be(900_000);
        d.Postings.Single(p => p.AccountCode == AccountCodes.TutorPayable).OwnerId.Should().Be(Tutor);
    }

    [Fact]
    public void Entry2_settlement_nets_the_gateway_fee()
    {
        var intent = CourseSale();
        var fee = FakePaymentGateway.Fee(intent.Amount);
        fee.Should().Be(M.NairaAmount(775), "1.5% + ₦100 on ₦45,000");
        var d = PaymentPostings.Entry2Settlement(intent, fee, Now);
        LedgerService.Validate(d).IsSuccess.Should().BeTrue();
        Dr(d, AccountCodes.BankClientFunds).Should().Be(4_422_500);
        Dr(d, AccountCodes.ProcessingFees).Should().Be(77_500);
        Cr(d, AccountCodes.GatewayReceivable).Should().Be(4_500_000);
    }

    [Fact]
    public void Entry3_sweep_moves_commission_net_of_fee()
    {
        var intent = CourseSale();
        intent.MarkSucceeded(FakePaymentGateway.Fee(intent.Amount), Now, Guid.NewGuid());
        intent.SweepableCommission.Should().Be(M.NairaAmount(8_225));
        var d = PaymentPostings.Entry3Sweep("batch", intent.SweepableCommission, Now);
        Dr(d, AccountCodes.BankOperating).Should().Be(822_500);
        Cr(d, AccountCodes.BankClientFunds).Should().Be(822_500);
    }

    [Fact]
    public void Entry5_full_refund_reverses_both_shares_from_client_funds_once_settled()
    {
        var intent = CourseSale();
        intent.MarkSucceeded(M.NairaAmount(775), Now, Guid.NewGuid());
        intent.MarkSettled(M.NairaAmount(775), Now.AddDays(1));
        var refund = Refund.Request(Tenants.Marketplace, intent.Id, M.NairaAmount(45_000), "Changed mind", Guid.NewGuid());
        var d = PaymentPostings.Entry5Refund(intent, refund, Now.AddDays(2));
        LedgerService.Validate(d).IsSuccess.Should().BeTrue();
        Dr(d, AccountCodes.TutorPayable).Should().Be(3_600_000);
        Dr(d, AccountCodes.CourseCommission).Should().Be(900_000);
        Cr(d, AccountCodes.BankClientFunds).Should().Be(4_500_000);

        var partial = Refund.Request(Tenants.Marketplace, intent.Id, M.NairaAmount(4_500), "10%", Guid.NewGuid());
        var p = PaymentPostings.Entry5Refund(intent, partial, Now);
        Dr(p, AccountCodes.TutorPayable).Should().Be(360_000);
        Dr(p, AccountCodes.CourseCommission).Should().Be(90_000);
    }

    [Fact]
    public void Entry6_invoice_credits_programme_funds_and_vat()
    {
        var invoice = Invoice.Issue(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), "INV-2026-00001", M.NairaAmount(90_000_000), Invoice.VatOn(M.NairaAmount(15_000_000)), Now, 14, Guid.NewGuid());
        invoice.Vat.Should().Be(M.NairaAmount(1_125_000));
        invoice.Total.Should().Be(M.NairaAmount(91_125_000));
        var d = PaymentPostings.Entry6InvoicePaid(invoice, invoice.Total, Now);
        LedgerService.Validate(d).IsSuccess.Should().BeTrue();
        Dr(d, AccountCodes.BankClientFunds).Should().Be(9_112_500_000);
        Cr(d, AccountCodes.ProgrammeFundsHeld).Should().Be(9_000_000_000);
        Cr(d, AccountCodes.VatPayable).Should().Be(112_500_000);
    }

    [Fact]
    public void Invoice_transfer_matching_flags_partial_and_holds_overpayment()
    {
        var invoice = Invoice.Issue(Tenants.Marketplace, Guid.NewGuid(), null, "INV-2026-00002", M.NairaAmount(100_000), M.Zero(), Now, 14, Guid.NewGuid());
        invoice.ApplyTransfer(M.NairaAmount(40_000), Now, Guid.NewGuid()).Value.Should().Be(TransferMatch.Partial);
        invoice.Status.Should().Be(InvoiceStatus.PartiallyPaid);
        invoice.Outstanding.Should().Be(M.NairaAmount(60_000));
        invoice.ApplyTransfer(M.NairaAmount(70_000), Now, Guid.NewGuid()).Value.Should().Be(TransferMatch.Overpaid);
        invoice.Status.Should().Be(InvoiceStatus.Held);
        var second = Invoice.Issue(Tenants.Marketplace, Guid.NewGuid(), null, "INV-2026-00003", M.NairaAmount(100_000), M.Zero(), Now, 14, Guid.NewGuid());
        second.ApplyTransfer(M.NairaAmount(100_000), Now, Guid.NewGuid()).Value.Should().Be(TransferMatch.Matched);
        second.Status.Should().Be(InvoiceStatus.Paid);
        second.ApplyTransfer(M.NairaAmount(1), Now, Guid.NewGuid()).Error.Code.Should().Be("invoice_already_paid");
    }

    [Fact]
    public void Refunds_above_100k_need_a_different_approver()
    {
        var maker = Guid.NewGuid();
        var small = Refund.Request(Tenants.Marketplace, Guid.NewGuid(), M.NairaAmount(100_000), "ok", maker);
        small.NeedsSecondApprover.Should().BeFalse();
        small.Approve(maker).IsSuccess.Should().BeTrue();

        var large = Refund.Request(Tenants.Marketplace, Guid.NewGuid(), M.NairaAmount(100_001), "big", maker);
        large.NeedsSecondApprover.Should().BeTrue();
        large.Approve(maker).Error.Code.Should().Be("four_eyes_required");
        large.Approve(Guid.NewGuid()).IsSuccess.Should().BeTrue();
        large.Approve(Guid.NewGuid()).IsSuccess.Should().BeTrue("approving an already approved but unissued refund is idempotent");
        large.MarkIssued("rf_1", Guid.NewGuid(), Now);
        large.Approve(Guid.NewGuid()).Error.Code.Should().Be("refund_not_pending");
    }

    [Fact]
    public void Fee_rules_follow_the_blueprint_rates()
    {
        // Without an approved fee rule (Sprint 8 back office), the defaults apply.
        var options = new FeeOptions();
        static int Bps(SaleContext c, FeeOptions o) => FeeRules.DefaultBps(FeeRules.CodeFor(c), o);
        Bps(new SaleContext(PaymentPurposes.Course, LearnerSources.Platform), options).Should().Be(2000);
        Bps(new SaleContext(PaymentPurposes.Course, LearnerSources.TutorReferred), options).Should().Be(800);
        Bps(new SaleContext(PaymentPurposes.Booking, LearnerSources.Platform), options).Should().Be(1500);
        Bps(new SaleContext(PaymentPurposes.CoPay, LearnerSources.Platform), options).Should().Be(0);
    }

    [Fact]
    public void Payment_intent_confirmation_is_idempotent_and_failure_only_from_pending()
    {
        var intent = CourseSale();
        intent.MarkSucceeded(M.Zero(), Now, Guid.NewGuid()).Should().BeTrue();
        intent.MarkSucceeded(M.Zero(), Now, Guid.NewGuid()).Should().BeFalse();
        intent.MarkFailed("late", Now).Should().BeFalse();
        intent.MarkSettled(M.Zero(), Now).IsSuccess.Should().BeTrue();
        intent.MarkSettled(M.Zero(), Now).Error.Code.Should().Be("payment_already_settled");
    }

    [Fact]
    public void Gateway_fee_schedule_matches_paystack_local_pricing()
    {
        FakePaymentGateway.Fee(M.NairaAmount(2_000)).Should().Be(M.NairaAmount(30));
        FakePaymentGateway.Fee(M.NairaAmount(10_000)).Should().Be(M.NairaAmount(250));
        FakePaymentGateway.Fee(M.NairaAmount(500_000)).Should().Be(M.NairaAmount(2_000), "capped");
    }
}

/// <summary>ADR 0018 entries: 11 (units bought), 12 (sale paid with units), 5 back to units and 6 paid with units all balance and hit 2050.</summary>
public class UnitPostingsTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Tutor = Guid.CreateVersion7();
    private static readonly Guid WalletId = Guid.CreateVersion7();

    private static long Dr(JournalEntryDraft d, string code) => d.Postings.Where(p => p.AccountCode == code).Sum(p => p.Debit.AmountKobo);
    private static long Cr(JournalEntryDraft d, string code) => d.Postings.Where(p => p.AccountCode == code).Sum(p => p.Credit.AmountKobo);

    private static PaymentIntent UnitsSale(long unitsValueKobo)
    {
        var intent = PaymentIntent.Create(Tenants.Marketplace, Guid.CreateVersion7(), PaymentPurposes.Course, Guid.CreateVersion7(), "q_1", "Course", M.NairaAmount(45_000), Tutor, 2000, "course_platform");
        intent.AttachUnits(WalletId, 45, 100_000, unitsValueKobo);
        return intent;
    }

    [Fact]
    public void Entry11_unit_purchase_holds_the_cash_as_a_unit_liability()
    {
        var intent = PaymentIntent.Create(Tenants.Marketplace, Guid.CreateVersion7(), PaymentPurposes.Units, WalletId, null, "50 units", M.NairaAmount(50_000), null, 0, "units");
        intent.AttachCheckout("fake", "fk_units", "https://checkout");
        intent.Commission.Should().Be(M.Zero());
        var d = PaymentPostings.Entry11UnitPurchase(intent, Now);
        LedgerService.Validate(d).IsSuccess.Should().BeTrue();
        Dr(d, AccountCodes.GatewayReceivable).Should().Be(5_000_000);
        Cr(d, AccountCodes.UnitBalancesHeld).Should().Be(5_000_000);
        d.Postings.Single(p => p.AccountCode == AccountCodes.UnitBalancesHeld).OwnerId.Should().Be(WalletId);
        d.Kind.Should().Be("unit_purchase");
    }

    [Fact]
    public void Entry12_redemption_releases_the_units_value_and_splits_the_price_like_a_card_sale()
    {
        var d = PaymentPostings.Entry12UnitRedemption(UnitsSale(4_500_000), Now);
        LedgerService.Validate(d).IsSuccess.Should().BeTrue();
        Dr(d, AccountCodes.UnitBalancesHeld).Should().Be(4_500_000);
        Cr(d, AccountCodes.TutorPayable).Should().Be(3_600_000);
        Cr(d, AccountCodes.CourseCommission).Should().Be(900_000);
        d.Postings.Should().NotContain(p => p.AccountCode == AccountCodes.UnitRevaluation);
    }

    [Fact]
    public void Entry12_books_a_revaluation_gain_when_units_were_bought_dearer()
    {
        var d = PaymentPostings.Entry12UnitRedemption(UnitsSale(4_600_000), Now);
        LedgerService.Validate(d).IsSuccess.Should().BeTrue();
        Dr(d, AccountCodes.UnitBalancesHeld).Should().Be(4_600_000);
        Cr(d, AccountCodes.UnitRevaluation).Should().Be(100_000);
    }

    [Fact]
    public void Entry12_books_a_revaluation_cost_when_units_were_bought_cheaper()
    {
        var d = PaymentPostings.Entry12UnitRedemption(UnitsSale(4_400_000), Now);
        LedgerService.Validate(d).IsSuccess.Should().BeTrue();
        Dr(d, AccountCodes.UnitRevaluation).Should().Be(100_000);
    }

    [Fact]
    public void Entry5_refund_of_a_units_sale_restores_the_unit_liability()
    {
        var intent = UnitsSale(4_500_000);
        var refund = Refund.Request(Tenants.Marketplace, intent.Id, M.NairaAmount(9_000), "changed mind", Guid.CreateVersion7());
        refund.RedirectToUnits();
        var d = PaymentPostings.Entry5Refund(intent, refund, Now);
        LedgerService.Validate(d).IsSuccess.Should().BeTrue();
        Cr(d, AccountCodes.UnitBalancesHeld).Should().Be(900_000);
        Dr(d, AccountCodes.TutorPayable).Should().Be(720_000);
        Dr(d, AccountCodes.CourseCommission).Should().Be(180_000);
        intent.UnitsToRefund(M.NairaAmount(9_000)).Should().Be(9);
    }

    [Fact]
    public void Entry6_invoice_paid_with_units_funds_the_programme_and_the_vat()
    {
        var subtotal = M.NairaAmount(1_000_000);
        var vat = M.NairaAmount(75_000);
        var invoice = Invoice.Issue(Tenants.Marketplace, Guid.CreateVersion7(), Guid.CreateVersion7(), "INV-1", subtotal, vat, Now, 14, Guid.CreateVersion7());
        var d = PaymentPostings.Entry6InvoicePaidFromUnits(invoice, invoice.Total, M.NairaAmount(1_075_000), WalletId, Now);
        LedgerService.Validate(d).IsSuccess.Should().BeTrue();
        Dr(d, AccountCodes.UnitBalancesHeld).Should().Be(107_500_000);
        Cr(d, AccountCodes.ProgrammeFundsHeld).Should().Be(100_000_000);
        Cr(d, AccountCodes.VatPayable).Should().Be(7_500_000);
    }
}
