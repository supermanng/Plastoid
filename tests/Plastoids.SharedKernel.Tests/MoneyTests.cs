using Plastoids.SharedKernel.Domain;

namespace Plastoids.SharedKernel.Tests;

public class MoneyTests
{
    [Fact]
    public void Naira_amount_is_stored_in_kobo()
    {
        Money.NairaAmount(45_000).AmountKobo.Should().Be(4_500_000);
        Money.NairaAmount(45_000).Currency.Should().Be("NGN");
    }

    [Fact]
    public void Addition_and_subtraction_keep_currency()
    {
        var a = Money.Kobo(1_000);
        var b = Money.Kobo(250);
        (a + b).AmountKobo.Should().Be(1_250);
        (a - b).AmountKobo.Should().Be(750);
        (-b).AmountKobo.Should().Be(-250);
    }

    [Fact]
    public void Cross_currency_arithmetic_throws()
    {
        var ngn = Money.Kobo(100);
        var usd = new Money(100, "USD");
        var act = () => ngn + usd;
        act.Should().Throw<InvalidOperationException>().WithMessage("*Currency mismatch*");
    }

    [Theory]
    [InlineData(4_500_000, 20, 900_000)]   // 20% commission on ₦45,000 = ₦9,000 (blueprint §5)
    [InlineData(4_500_000, 8, 360_000)]    // 8% tutor-referred
    [InlineData(15_000_000, 12, 1_800_000)] // 12% management fee on ₦150,000 seat
    [InlineData(1_500_000_000, 7.5, 112_500_000)] // 7.5% VAT on ₦15m fees = ₦1,125,000
    public void Percent_matches_blueprint_fee_examples(long kobo, double percent, long expected)
    {
        Money.Kobo(kobo).Percent((decimal)percent).AmountKobo.Should().Be(expected);
    }

    [Fact]
    public void Percent_uses_bankers_rounding_by_default()
    {
        Money.Kobo(125).Percent(10).AmountKobo.Should().Be(12); // 12.5 → 12
        Money.Kobo(135).Percent(10).AmountKobo.Should().Be(14); // 13.5 → 14
        Money.Kobo(125).Percent(10, MidpointRounding.AwayFromZero).AmountKobo.Should().Be(13);
    }

    [Fact]
    public void Allocate_never_loses_a_kobo()
    {
        var shares = Money.Kobo(100).Allocate(3);
        shares.Select(s => s.AmountKobo).Should().Equal(34, 33, 33);
        shares.Sum(s => s.AmountKobo).Should().Be(100);
    }

    [Fact]
    public void Comparison_operators_work()
    {
        (Money.Kobo(5) > Money.Kobo(4)).Should().BeTrue();
        (Money.Kobo(5) <= Money.Kobo(5)).Should().BeTrue();
        Money.Zero().IsZero.Should().BeTrue();
        Money.Kobo(-1).IsNegative.Should().BeTrue();
    }

    [Fact]
    public void Currency_must_be_three_letters()
    {
        var act = () => new Money(1, "NAIRA");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToString_is_human_readable()
    {
        Money.Kobo(4_500_075).ToString().Should().Be("NGN 45000.75");
    }
}
