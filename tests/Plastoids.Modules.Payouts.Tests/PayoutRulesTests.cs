using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payouts.Domain;
using Plastoids.Modules.Payouts.Services;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Payouts.Tests;

public class PayoutRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 9, 0, 0, DateTimeKind.Utc); // a Thursday

    [Fact]
    public void Maker_cannot_be_checker()
    {
        var maker = Guid.NewGuid();
        var batch = PayoutBatch.Create(Tenants.Marketplace, "PB-2026-W48-001", maker, 1, Money.NairaAmount(36_000));
        batch.Approve(maker, Now).Error.Code.Should().Be("maker_checker_violation");
        batch.Status.Should().Be(BatchStatus.PendingApproval);
        batch.MarkSubmitted("fake", Now).Error.Code.Should().Be("batch_not_approved");
        batch.Approve(Guid.NewGuid(), Now).IsSuccess.Should().BeTrue();
        batch.Approve(Guid.NewGuid(), Now).Error.Code.Should().Be("batch_not_pending");
        batch.MarkSubmitted("fake", Now).IsSuccess.Should().BeTrue();
        batch.Reject(Guid.NewGuid(), "late").Error.Code.Should().Be("batch_not_pending");
    }

    [Fact]
    public void Earnings_hold_release_and_refund()
    {
        var sale = Earning.Record(Tenants.Marketplace, PayeeTypes.Tutor, Guid.NewGuid(), EarningSources.Course, Guid.NewGuid(), Guid.NewGuid(), "payment:x", Money.NairaAmount(45_000), Money.NairaAmount(36_000), Now, Now.AddDays(7));
        sale.Commission.Should().Be(Money.NairaAmount(9_000));
        sale.Status.Should().Be(EarningStatus.Held);
        sale.ApplyRefund(Money.NairaAmount(22_500));
        sale.PayableNet.Should().Be(Money.NairaAmount(18_000), "half the gross refunded → half the net");
        sale.ApplyRefund(Money.NairaAmount(45_000));
        sale.PayableNet.Should().Be(Money.Zero(), "refunds never exceed the net");
        sale.Release(Now.AddDays(7)).Should().BeTrue();
        sale.Release(Now.AddDays(8)).Should().BeFalse();
    }

    [Fact]
    public void Payable_is_released_net_less_paid_and_pending_capped_by_the_ledger()
    {
        var tutor = Guid.NewGuid();
        var earnings = new List<Earning>
        {
            Released(tutor, 36_000),
            Released(tutor, 10_000),
            Earning.Record(Tenants.Marketplace, PayeeTypes.Tutor, tutor, EarningSources.Booking, Guid.NewGuid(), Guid.NewGuid(), "payment:held", Money.NairaAmount(10_000), Money.NairaAmount(8_500), Now, null),
        };
        var batch = Guid.NewGuid();
        var paid = Payout.Create(Tenants.Marketplace, batch, PayeeTypes.Tutor, tutor, Money.NairaAmount(10_000), "058", "******1234", "COURSE AUTHOR");
        paid.MarkSubmitted("trf_1", Now);
        paid.Settle("NIP1", Guid.NewGuid(), Now).IsSuccess.Should().BeTrue();
        var pending = Payout.Create(Tenants.Marketplace, batch, PayeeTypes.Tutor, tutor, Money.NairaAmount(6_000), "058", "******1234", "COURSE AUTHOR");

        var summary = PayableCalculator.Summarise(earnings, [paid, pending], false, Money.NairaAmount(44_500));
        summary.Gross.Should().Be(Money.NairaAmount(45_000 + 12_500 + 10_000));
        summary.Held.Should().Be(Money.NairaAmount(8_500));
        summary.Paid.Should().Be(Money.NairaAmount(10_000));
        summary.Pending.Should().Be(Money.NairaAmount(6_000));
        summary.Payable.Should().Be(Money.NairaAmount(30_000), "46,000 released − 10,000 paid − 6,000 pending");

        PayableCalculator.Summarise(earnings, [paid, pending], false, Money.NairaAmount(20_000)).Payable.Should().Be(Money.NairaAmount(14_000), "the ledger's 2010 balance (less pending) is the cap");
        PayableCalculator.Summarise(earnings, [paid, pending], false, Money.NairaAmount(5_000)).Payable.Should().Be(Money.Zero(), "never negative");

        // A reversal restores the balance: the settled payout no longer counts as paid.
        paid.Reverse("account closed", Guid.NewGuid(), Now).IsSuccess.Should().BeTrue();
        PayableCalculator.Summarise(earnings, [paid, pending], false, Money.NairaAmount(54_500)).Payable.Should().Be(Money.NairaAmount(40_000), "the reversal entry also restores 2010");
        paid.Reverse("again", Guid.NewGuid(), Now).Error.Code.Should().Be("payout_not_settled");
    }

    [Fact]
    public void Entry_4_and_its_reversal_balance()
    {
        var payout = Payout.Create(Tenants.Marketplace, Guid.NewGuid(), PayeeTypes.Tutor, Guid.NewGuid(), Money.NairaAmount(36_000), "058", "******1234", "COURSE AUTHOR");
        payout.MarkSubmitted("trf_x", Now);
        payout.TutorUserId.Should().Be(payout.PayeeId);
        var entry = PayoutPostings.Entry4Settled(payout, Now);
        entry.Kind.Should().Be("tutor_payout");
        entry.Postings.Sum(p => p.Debit.AmountKobo).Should().Be(entry.Postings.Sum(p => p.Credit.AmountKobo));
        entry.Postings.Single(p => p.AccountCode == AccountCodes.TutorPayable).OwnerId.Should().Be(payout.PayeeId);
        entry.Postings.Single(p => p.AccountCode == AccountCodes.TutorPayable).OwnerType.Should().Be(SubLedgerOwnerTypes.Tutor);
        entry.Postings.Single(p => p.AccountCode == AccountCodes.BankClientFunds).Credit.Should().Be(Money.NairaAmount(36_000));
        var reversal = PayoutPostings.Reversal(payout, Now);
        reversal.Postings.Single(p => p.AccountCode == AccountCodes.TutorPayable).Credit.Should().Be(Money.NairaAmount(36_000));
    }

    [Fact]
    public void Entry_4_provider_variant_pays_the_organisation_from_2060()
    {
        var organisation = Guid.NewGuid();
        var payout = Payout.Create(Tenants.Marketplace, Guid.NewGuid(), PayeeTypes.Organisation, organisation, Money.NairaAmount(50_000), "058", "******5678", "ACME TRAINING LTD");
        payout.MarkSubmitted("trf_o", Now);
        payout.TutorUserId.Should().BeNull("an organisation has no single recipient");
        var entry = PayoutPostings.Entry4Settled(payout, Now);
        entry.Kind.Should().Be("provider_payout");
        entry.Postings.Sum(p => p.Debit.AmountKobo).Should().Be(entry.Postings.Sum(p => p.Credit.AmountKobo));
        entry.Postings.Should().NotContain(p => p.AccountCode == AccountCodes.TutorPayable);
        var provider = entry.Postings.Single(p => p.AccountCode == AccountCodes.ProviderPayable);
        provider.Debit.Should().Be(Money.NairaAmount(50_000));
        provider.OwnerType.Should().Be(SubLedgerOwnerTypes.Organisation);
        provider.OwnerId.Should().Be(organisation);
        entry.Postings.Single(p => p.AccountCode == AccountCodes.BankClientFunds).Credit.Should().Be(Money.NairaAmount(50_000));

        var reversal = PayoutPostings.Reversal(payout, Now);
        reversal.Kind.Should().Be("provider_payout_reversal");
        reversal.Postings.Single(p => p.AccountCode == AccountCodes.ProviderPayable).Credit.Should().Be(Money.NairaAmount(50_000));
        reversal.Postings.Single(p => p.AccountCode == AccountCodes.ProviderPayable).OwnerId.Should().Be(organisation);
    }

    [Fact]
    public void Organisation_and_tutor_earnings_from_one_sale_are_summarised_apart()
    {
        var organisation = Guid.NewGuid();
        var tutor = Guid.NewGuid();
        var sale = Guid.NewGuid();
        // ₦45,000 sale at 15%: commission 6,750; net 38,250; tutor share 40% of net = 15,300; organisation 22,950.
        var organisationEarning = Earning.Record(Tenants.Marketplace, PayeeTypes.Organisation, organisation, EarningSources.Course, sale, Guid.NewGuid(), "payment:s", Money.NairaAmount(45_000), Money.NairaAmount(22_950), Now, Now);
        var tutorEarning = Earning.Record(Tenants.Marketplace, PayeeTypes.Tutor, tutor, EarningSources.Course, sale, Guid.NewGuid(), "payment:s:tutor", Money.NairaAmount(45_000), Money.NairaAmount(15_300), Now, Now);
        organisationEarning.Release(Now);
        tutorEarning.Release(Now);

        var forOrganisation = PayableCalculator.Summarise([organisationEarning], [], false, Money.NairaAmount(22_950));
        forOrganisation.Net.Should().Be(Money.NairaAmount(22_950));
        forOrganisation.Payable.Should().Be(Money.NairaAmount(22_950));
        forOrganisation.Commission.Should().Be(Money.NairaAmount(22_050), "the row's commission is gross − its net; the tutor share sits on the tutor's row");

        var forTutor = PayableCalculator.Summarise([tutorEarning], [], false, Money.NairaAmount(15_300));
        forTutor.Payable.Should().Be(Money.NairaAmount(15_300));

        // A half refund reduces both rows pro rata (RefundIssuedConsumer applies it to every row of the sale).
        organisationEarning.ApplyRefund(Money.NairaAmount(22_500));
        tutorEarning.ApplyRefund(Money.NairaAmount(22_500));
        organisationEarning.PayableNet.Should().Be(Money.NairaAmount(11_475));
        tutorEarning.PayableNet.Should().Be(Money.NairaAmount(7_650));
    }

    [Fact]
    public void Organisation_payout_account_name_must_match_the_registered_name()
    {
        OrganisationPayoutAccount.NamesMatch("Acme Training Limited", "ACME TRAINING LIMITED").Should().BeTrue();
        OrganisationPayoutAccount.NamesMatch("Acme Training Limited", "ACME TRAINING LTD").Should().BeTrue("one token of slack");
        OrganisationPayoutAccount.NamesMatch("Acme Training Limited", "JOHN DOE").Should().BeFalse();
        OrganisationPayoutAccount.NamesMatch("Acme Training", string.Empty).Should().BeFalse();
    }

    [Fact]
    public void Dispute_holds_release_once_and_may_target_an_organisation()
    {
        var organisationHold = DisputeHold.Place(Tenants.Marketplace, PayeeTypes.Organisation, Guid.NewGuid(), "Chargeback under review", Guid.NewGuid(), Now, Now.AddDays(7));
        organisationHold.PayeeType.Should().Be(PayeeTypes.Organisation);
        organisationHold.IsOpen.Should().BeTrue();

        var hold = DisputeHold.Place(Tenants.Marketplace, PayeeTypes.Tutor, Guid.NewGuid(), "Learner dispute on session 42", Guid.NewGuid(), Now, Now.AddDays(7));
        hold.IsOpen.Should().BeTrue();
        hold.Release(Guid.NewGuid(), "Resolved in the tutor's favour", Now.AddDays(1)).IsSuccess.Should().BeTrue();
        hold.Release(Guid.NewGuid(), "again", Now.AddDays(2)).Error.Code.Should().Be("hold_already_released");
    }

    private static Earning Released(Guid tutor, long net)
    {
        var e = Earning.Record(Tenants.Marketplace, PayeeTypes.Tutor, tutor, EarningSources.Course, Guid.NewGuid(), Guid.NewGuid(), "payment:" + Guid.NewGuid().ToString("N"), Money.NairaAmount(net == 36_000 ? 45_000 : 12_500), Money.NairaAmount(net), Now, Now);
        e.Release(Now);
        return e;
    }
}
