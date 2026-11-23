using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Sponsorship.Tests;

/// <summary>ADR 0021 seat blocks: open seats, release limits, pro rata units, close-out and the balance of entries 14–16.</summary>
public class SeatBlockTests
{
    private static readonly DateTime Now = new(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OrganisationId = Guid.NewGuid();
    private static readonly Guid TutorUserId = Guid.NewGuid();
    private static readonly Money Price = Money.NairaAmount(45_000);

    private static SeatBlockCohort Cohort() => new(Guid.NewGuid(), "January cohort", Guid.NewGuid(), "Data Analytics", "data-analytics", Guid.NewGuid(), TutorUserId, Now.AddDays(14), Now.AddDays(98));

    private static SeatBlock Block(int seats = 3, long units = 135)
    {
        var block = SeatBlock.Purchase(Tenants.Marketplace, OrganisationId, "Lagos Data School", Cohort(), seats, Price, Guid.NewGuid(), Now).Value;
        block.Fund(units, 100_000, Money.Kobo(units * 100_000), Guid.NewGuid());
        block.LinkLedgerEntry(Guid.NewGuid());
        return block;
    }

    private static SeatAllocation Invited(SeatBlock block, int seatNo) => SeatAllocation.Invite(Tenants.Marketplace, block, seatNo, "Ada", ContactKinds.Phone, "HASH", null, "+2348031****67", Guid.NewGuid(), Now);

    [Fact]
    public void Purchase_validates_the_seat_count()
    {
        SeatBlock.Purchase(Tenants.Marketplace, OrganisationId, "Sponsor", Cohort(), 0, Price, Guid.NewGuid(), Now).Error.Code.Should().Be("seats_invalid");
        SeatBlock.Purchase(Tenants.Marketplace, OrganisationId, "Sponsor", Cohort(), 1001, Price, Guid.NewGuid(), Now).Error.Code.Should().Be("seats_invalid");
        var block = Block();
        block.Status.Should().Be(SeatBlockStatus.Active);
        block.Open.Should().Be(3);
        block.TotalPrice.Should().Be(Money.NairaAmount(135_000));
        block.UnitsRedeemed.Should().Be(135);
    }

    [Fact]
    public void Seats_are_taken_in_order_until_none_is_open()
    {
        var block = Block();
        block.TakeSeat().Value.Should().Be(1);
        block.TakeSeat().Value.Should().Be(2);
        block.Release(1).IsSuccess.Should().BeTrue();
        block.Open.Should().Be(0);
        block.TakeSeat().Error.Code.Should().Be("no_open_seats");
        block.Allocated.Should().Be(2);
        block.Released.Should().Be(1);
    }

    [Fact]
    public void Release_never_exceeds_the_open_seats()
    {
        var block = Block();
        block.TakeSeat();
        block.Release(3).Error.Code.Should().Be("release_exceeds_open");
        block.Release(0).Error.Code.Should().Be("seats_invalid");
        block.Release(2).IsSuccess.Should().BeTrue();
        block.Open.Should().Be(0);
        block.Release(1).Error.Code.Should().Be("release_exceeds_open");
    }

    [Theory]
    [InlineData(3, 135, 1, 45)]
    [InlineData(3, 100, 1, 33)]
    [InlineData(3, 100, 2, 67)]
    [InlineData(4, 10, 1, 2)] // 2.5 → 2 (half to even)
    [InlineData(4, 10, 3, 8)] // 7.5 → 8 (half to even)
    [InlineData(4, 10, 4, 10)]
    public void Units_are_pro_rata_to_the_seats_released(int seats, long units, int released, long expected)
    {
        Block(seats, units).UnitsFor(released).Should().Be(expected);
    }

    [Fact]
    public void Closing_stops_allocation_and_release()
    {
        var block = Block();
        block.Close(Now.AddDays(100));
        block.Status.Should().Be(SeatBlockStatus.Closed);
        block.ClosedAt.Should().Be(Now.AddDays(100));
        block.TakeSeat().Error.Code.Should().Be("block_closed");
        block.Release(1).Error.Code.Should().Be("block_closed");
        block.Close(Now.AddDays(101));
        block.ClosedAt.Should().Be(Now.AddDays(100), "closing twice keeps the first close time");
    }

    [Fact]
    public void Allocation_enrols_once_and_keeps_the_split()
    {
        var block = Block();
        var allocation = Invited(block, block.TakeSeat().Value);
        allocation.Status.Should().Be(SeatAllocationStatus.Invited);
        allocation.Commission.Should().Be(Money.Zero());
        var learner = Guid.NewGuid();
        var split = new SeatSplit(Money.NairaAmount(9_000), Money.Zero(), Money.NairaAmount(36_000), PayeeTypes.Tutor, TutorUserId);
        allocation.Enrol(learner, split, null, Now).IsSuccess.Should().BeTrue();
        allocation.LinkLedgerEntry(Guid.NewGuid());
        allocation.Status.Should().Be(SeatAllocationStatus.Enrolled);
        allocation.LearnerId.Should().Be(learner);
        allocation.PayeeNet.Should().Be(Money.NairaAmount(36_000));
        allocation.LedgerEntryId.Should().NotBeNull();
        allocation.Enrol(learner, split, null, Now).IsSuccess.Should().BeTrue("re-enrolling the same learner is a no-op");
        allocation.Enrol(Guid.NewGuid(), split, null, Now).Error.Code.Should().Be("seat_already_enrolled");
    }

    [Fact]
    public void Split_of_a_tutor_course_pays_the_owner_the_net()
    {
        var owner = Guid.NewGuid();
        var split = SeatPricing.Compute(Price, 2000, CourseOwnerTypes.Tutor, owner, null, null);
        split.Commission.Should().Be(Money.NairaAmount(9_000));
        split.TutorShare.Should().Be(Money.Zero());
        split.PayeeNet.Should().Be(Money.NairaAmount(36_000));
        split.PayeeType.Should().Be(PayeeTypes.Tutor);
        split.PayeeId.Should().Be(owner);
    }

    [Fact]
    public void Split_of_an_organisation_course_shares_the_net_with_the_engaged_tutor()
    {
        var split = SeatPricing.Compute(Price, 1500, CourseOwnerTypes.Organisation, Guid.NewGuid(), OrganisationId, new EngagementTerms(EngagementTermKinds.RevenueShare, 4000, null));
        split.Commission.Should().Be(Money.NairaAmount(6_750));
        split.TutorShare.Should().Be(Money.NairaAmount(15_300), "40 % of the ₦38,250 net");
        split.PayeeNet.Should().Be(Money.NairaAmount(22_950));
        split.PayeeType.Should().Be(PayeeTypes.Organisation);
        split.PayeeId.Should().Be(OrganisationId);
        (split.Commission + split.TutorShare + split.PayeeNet).Should().Be(Price);

        var noTerms = SeatPricing.Compute(Price, 1500, CourseOwnerTypes.Organisation, Guid.NewGuid(), OrganisationId, null);
        noTerms.TutorShare.Should().Be(Money.Zero());
        noTerms.PayeeNet.Should().Be(Money.NairaAmount(38_250));
    }

    [Fact]
    public void Entry_14_with_goodwill_units_has_no_wallet_line_and_expenses_the_block_to_revaluation()
    {
        // A staff adjustment credits zero-value lots (ADR 0018): nothing leaves 2050, the block is funded by a 4060 cost.
        var block = Block();
        var goodwill = SponsorshipPostings.Entry14SeatBlockPurchase(block, Money.Zero(), Guid.NewGuid(), Now);
        Balanced(goodwill);
        goodwill.Postings.Should().NotContain(p => p.AccountCode == AccountCodes.UnitBalancesHeld);
        goodwill.Postings.Single(p => p.AccountCode == AccountCodes.UnitRevaluation).Debit.Should().Be(block.TotalPrice);
        goodwill.Postings.Single(p => p.AccountCode == AccountCodes.ProgrammeFundsHeld).Credit.Should().Be(block.TotalPrice);
    }

    [Fact]
    public void Entry_14_moves_the_units_value_into_the_block_with_any_gap_to_revaluation()
    {
        var block = Block();
        var walletId = Guid.NewGuid();
        var exact = SponsorshipPostings.Entry14SeatBlockPurchase(block, Money.NairaAmount(135_000), walletId, Now);
        Balanced(exact);
        exact.Reference.Should().Be($"seat-block:{block.Id:N}:purchase");
        exact.Postings.Should().HaveCount(2);
        var debit = exact.Postings.Single(p => p.AccountCode == AccountCodes.UnitBalancesHeld);
        debit.Debit.Should().Be(Money.NairaAmount(135_000));
        debit.OwnerType.Should().Be(SubLedgerOwnerTypes.Wallet);
        debit.OwnerId.Should().Be(walletId);
        var credit = exact.Postings.Single(p => p.AccountCode == AccountCodes.ProgrammeFundsHeld);
        credit.Credit.Should().Be(Money.NairaAmount(135_000));
        credit.OwnerType.Should().Be(SubLedgerOwnerTypes.Programme);
        credit.OwnerId.Should().Be(block.Id);

        // Lots were bought cheaper than today's rate: the wallet releases less naira than the block is worth → shortfall debited to 4060.
        var shortfall = SponsorshipPostings.Entry14SeatBlockPurchase(block, Money.NairaAmount(130_000), walletId, Now);
        Balanced(shortfall);
        shortfall.Postings.Single(p => p.AccountCode == AccountCodes.UnitRevaluation).Debit.Should().Be(Money.NairaAmount(5_000));

        var surplus = SponsorshipPostings.Entry14SeatBlockPurchase(block, Money.NairaAmount(140_000), walletId, Now);
        Balanced(surplus);
        surplus.Postings.Single(p => p.AccountCode == AccountCodes.UnitRevaluation).Credit.Should().Be(Money.NairaAmount(5_000));
    }

    [Fact]
    public void Entry_15_pays_commission_tutor_share_and_the_payee()
    {
        var block = Block();
        var tutorCourse = Invited(block, block.TakeSeat().Value);
        tutorCourse.Enrol(Guid.NewGuid(), new SeatSplit(Money.NairaAmount(9_000), Money.Zero(), Money.NairaAmount(36_000), PayeeTypes.Tutor, TutorUserId), null, Now);
        var entry = SponsorshipPostings.Entry15SeatAllocated(block, tutorCourse, Now);
        Balanced(entry);
        entry.Reference.Should().Be($"seat:{tutorCourse.Id:N}");
        entry.Postings.Single(p => p.AccountCode == AccountCodes.ProgrammeFundsHeld).Debit.Should().Be(Price);
        entry.Postings.Single(p => p.AccountCode == AccountCodes.CourseCommission).Credit.Should().Be(Money.NairaAmount(9_000));
        var tutor = entry.Postings.Single(p => p.AccountCode == AccountCodes.TutorPayable);
        tutor.Credit.Should().Be(Money.NairaAmount(36_000));
        tutor.OwnerId.Should().Be(TutorUserId);

        var organisationCourse = Invited(block, block.TakeSeat().Value);
        organisationCourse.Enrol(Guid.NewGuid(), new SeatSplit(Money.NairaAmount(6_750), Money.NairaAmount(15_300), Money.NairaAmount(22_950), PayeeTypes.Organisation, OrganisationId), null, Now);
        var provider = SponsorshipPostings.Entry15SeatAllocated(block, organisationCourse, Now);
        Balanced(provider);
        provider.Postings.Should().HaveCount(4);
        provider.Postings.Single(p => p.AccountCode == AccountCodes.TutorPayable).Credit.Should().Be(Money.NairaAmount(15_300));
        provider.Postings.Single(p => p.AccountCode == AccountCodes.TutorPayable).OwnerId.Should().Be(TutorUserId);
        var organisation = provider.Postings.Single(p => p.AccountCode == AccountCodes.ProviderPayable);
        organisation.Credit.Should().Be(Money.NairaAmount(22_950));
        organisation.OwnerType.Should().Be(SubLedgerOwnerTypes.Organisation);
        organisation.OwnerId.Should().Be(OrganisationId);
    }

    [Fact]
    public void Entry_16_returns_the_seats_value_to_the_wallet()
    {
        var block = Block();
        block.TakeSeat();
        var walletId = Guid.NewGuid();
        var entry = SponsorshipPostings.Entry16SeatsReleased(block, 2, block.PriceOf(2), walletId, Now);
        Balanced(entry);
        entry.Reference.Should().Be($"seat-block:{block.Id:N}:release:2");
        entry.Postings.Single(p => p.AccountCode == AccountCodes.ProgrammeFundsHeld).Debit.Should().Be(Money.NairaAmount(90_000));
        var wallet = entry.Postings.Single(p => p.AccountCode == AccountCodes.UnitBalancesHeld);
        wallet.Credit.Should().Be(Money.NairaAmount(90_000));
        wallet.OwnerId.Should().Be(walletId);

        SponsorshipPostings.Entry16SeatsReleased(block, 2, block.PriceOf(2), walletId, Now, SeatReleaser.CloseoutSuffix).Reference.Should().Be($"seat-block:{block.Id:N}:release:closeout");
    }

    private static void Balanced(JournalEntryDraft draft)
    {
        draft.Postings.Sum(p => p.Debit.AmountKobo).Should().Be(draft.Postings.Sum(p => p.Credit.AmountKobo), draft.Reference);
        draft.Postings.Should().OnlyContain(p => p.Debit.IsZero != p.Credit.IsZero, "exactly one side of a posting is non-zero");
    }
}
