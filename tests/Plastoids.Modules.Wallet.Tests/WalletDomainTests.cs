using Plastoids.Modules.Wallet.Contracts;
using Plastoids.Modules.Wallet.Domain;
using Plastoids.SharedKernel.Tenancy;
using M = Plastoids.SharedKernel.Domain.Money;
using W = Plastoids.Modules.Wallet.Domain.Wallet;

namespace Plastoids.Modules.Wallet.Tests;

/// <summary>ADR 0018: units are a prepaid closed-loop balance; lots release the naira they were bought for, first-in first-out.</summary>
public class WalletDomainTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Rate_snapshot_rounds_units_up_so_a_price_is_always_covered()
    {
        var rate = new UnitRateSnapshot(100_000, 1, Now, "v1"); // ₦1,000 per unit
        rate.UnitsFor(M.NairaAmount(45_000)).Should().Be(45);
        rate.UnitsFor(M.NairaAmount(45_001)).Should().Be(46);
        rate.UnitsFor(M.NairaAmount(999)).Should().Be(1);
        rate.UnitsFor(M.Zero()).Should().Be(0);
        rate.ValueOf(46).Should().Be(M.NairaAmount(46_000));
    }

    [Fact]
    public void Wallet_only_opens_for_users_and_organisations()
    {
        W.Open(Tenants.Marketplace, WalletOwnerTypes.User, Guid.CreateVersion7()).IsSuccess.Should().BeTrue();
        W.Open(Tenants.Marketplace, WalletOwnerTypes.Organisation, Guid.CreateVersion7()).IsSuccess.Should().BeTrue();
        var bad = W.Open(Tenants.Marketplace, "tenant", Guid.CreateVersion7());
        bad.IsFailure.Should().BeTrue();
        bad.Error.Code.Should().Be("wallet_owner_type_invalid");
    }

    [Fact]
    public void Lot_consumption_is_pro_rata_and_the_last_unit_takes_the_rounding_residue()
    {
        // 3 units bought for ₦1,000 in total: 333.33 each. Taking 1, 1, 1 must release exactly ₦1,000 overall.
        var lot = UnitLot.Create(Tenants.Marketplace, Guid.CreateVersion7(), 3, M.NairaAmount(1_000), WalletTransactionKinds.Purchase, "purchase:1", Now);
        var first = lot.Consume(1);
        var second = lot.Consume(1);
        var third = lot.Consume(1);
        first.Units.Should().Be(1);
        (first.Value + second.Value + third.Value).Should().Be(M.NairaAmount(1_000));
        lot.UnitsRemaining.Should().Be(0);
        lot.ValueRemaining.Should().Be(M.Zero());
        lot.IsOpen.Should().BeFalse();
        lot.Consume(1).Units.Should().Be(0);
    }

    [Fact]
    public void Lot_consumption_never_takes_more_than_it_holds()
    {
        var lot = UnitLot.Create(Tenants.Marketplace, Guid.CreateVersion7(), 10, M.NairaAmount(10_000), WalletTransactionKinds.Purchase, "purchase:2", Now);
        var taken = lot.Consume(25);
        taken.Units.Should().Be(10);
        taken.Value.Should().Be(M.NairaAmount(10_000));
    }

    [Fact]
    public void Unit_rate_needs_a_second_pair_of_eyes()
    {
        var maker = Guid.CreateVersion7();
        var checker = Guid.CreateVersion7();
        var rate = UnitRate.Propose(Tenants.Marketplace, 150_000, Now, "Inflation adjustment", 1, maker, Now).Value;
        rate.Status.Should().Be(UnitRateStatus.Proposed);

        var self = rate.Approve(maker, Now);
        self.IsFailure.Should().BeTrue();
        self.Error.Code.Should().Be("maker_checker_violation");

        rate.Approve(checker, Now).IsSuccess.Should().BeTrue();
        rate.Status.Should().Be(UnitRateStatus.Approved);
        rate.DecidedBy.Should().Be(checker);
        rate.Approve(checker, Now).Error.Code.Should().Be("unit_rate_not_pending");
    }

    [Fact]
    public void Unit_rate_rejects_bad_values_and_past_effective_dates()
    {
        UnitRate.Propose(Tenants.Marketplace, 50, Now, "too cheap", 1, Guid.CreateVersion7(), Now).Error.Code.Should().Be("unit_rate_out_of_range");
        UnitRate.Propose(Tenants.Marketplace, 100_000, Now.AddDays(-1), "yesterday", 1, Guid.CreateVersion7(), Now).Error.Code.Should().Be("effective_from_past");
        UnitRate.Propose(Tenants.Marketplace, 100_000, Now, "   ", 1, Guid.CreateVersion7(), Now).Error.Code.Should().Be("rationale_required");
    }

    [Fact]
    public void Effective_rate_is_the_latest_approved_version_not_in_the_future()
    {
        var staff = Guid.CreateVersion7();
        var checker = Guid.CreateVersion7();
        var v1 = UnitRate.Propose(Tenants.Marketplace, 100_000, Now, "launch", 1, staff, Now).Value;
        v1.Approve(checker, Now);
        var v2 = UnitRate.Propose(Tenants.Marketplace, 120_000, Now.AddDays(7), "next week", 2, staff, Now).Value;
        v2.Approve(checker, Now);
        var v3 = UnitRate.Propose(Tenants.Marketplace, 90_000, Now.AddDays(1), "not approved", 3, staff, Now).Value;

        UnitRate.EffectiveAt([v1, v2, v3], Now.AddDays(2))!.RateKobo.Should().Be(100_000);
        UnitRate.EffectiveAt([v1, v2, v3], Now.AddDays(8))!.RateKobo.Should().Be(120_000);
        UnitRate.EffectiveAt([v2, v3], Now).Should().BeNull();
    }

    [Fact]
    public void Purchase_fixes_units_and_rate_when_checkout_starts()
    {
        var purchase = UnitPurchase.Start(Tenants.Marketplace, Guid.CreateVersion7(), Guid.CreateVersion7(), 50, 100_000, Guid.CreateVersion7());
        purchase.Amount.Should().Be(M.NairaAmount(50_000));
        purchase.Status.Should().Be(UnitPurchaseStatus.Pending);
        purchase.MarkCredited(Now).Should().BeTrue();
        purchase.MarkCredited(Now).Should().BeFalse("a replayed webhook must not credit twice");
        purchase.MarkFailed(Now);
        purchase.Status.Should().Be(UnitPurchaseStatus.Credited, "a credited purchase never flips to failed");
    }
}
