using Plastoids.Modules.Booking.Domain;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;
using B = Plastoids.Modules.Booking.Domain.Booking;

namespace Plastoids.Modules.Sessions.Tests;

public class BookingRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Tutor = Guid.CreateVersion7();
    private static readonly Guid TutorUser = Guid.CreateVersion7();
    private static readonly Guid Learner = Guid.CreateVersion7();

    /// <summary>A booking made and paid a day before its slot, so slots in the past (relative to Now) are still valid fixtures.</summary>
    private static B Confirmed(DateTime start)
    {
        var madeAt = start.AddDays(-1);
        var b = B.Create(Tenants.Marketplace, Tutor, TutorUser, Learner, "one_to_one", start, 60, Money.NairaAmount(8_000), null, null, madeAt).Value;
        b.Confirm(madeAt).Should().BeTrue();
        return b;
    }

    [Fact]
    public void Create_rejects_imminent_slots_and_self_booking()
    {
        B.Create(Tenants.Marketplace, Tutor, TutorUser, Learner, "one_to_one", Now.AddMinutes(20), 60, Money.NairaAmount(8_000), null, null, Now).Error.Code.Should().Be("slot_too_soon");
        B.Create(Tenants.Marketplace, Tutor, Learner, Learner, "one_to_one", Now.AddHours(2), 60, Money.NairaAmount(8_000), null, null, Now).Error.Code.Should().Be("self_booking");
        var ok = B.Create(Tenants.Marketplace, Tutor, TutorUser, Learner, "one_to_one", Now.AddHours(2), 60, Money.NairaAmount(8_000), null, null, Now).Value;
        ok.SlotKey.Should().Be(B.KeyFor(Tutor, Now.AddHours(2)));
        ok.Confirm(Now).Should().BeTrue();
        ok.Confirm(Now).Should().BeFalse("redelivered payment webhooks must not double-confirm");
    }

    [Fact]
    public void Learner_cancelling_more_than_24h_ahead_gets_a_full_refund()
    {
        var b = Confirmed(Now.AddHours(25));
        var outcome = b.Cancel(CancellationActors.Learner, "plans changed", false, Now).Value;
        outcome.RefundPercent.Should().Be(100);
        outcome.TutorStrike.Should().BeFalse();
        b.Status.Should().Be(BookingStatus.Cancelled);
        b.RefundAmount.Should().Be(Money.NairaAmount(8_000));
        b.SlotKey.Should().BeNull("the slot is free again");
    }

    [Fact]
    public void Learner_cancelling_within_24h_gets_half()
    {
        var b = Confirmed(Now.AddHours(23));
        b.Cancel(CancellationActors.Learner, "late", false, Now).Value.RefundPercent.Should().Be(50);
        b.RefundAmount.Should().Be(Money.NairaAmount(4_000));
    }

    [Fact]
    public void Tutor_cancelling_always_refunds_in_full_without_a_strike()
    {
        var b = Confirmed(Now.AddHours(1));
        var outcome = b.Cancel(CancellationActors.Tutor, "ill", false, Now).Value;
        outcome.RefundPercent.Should().Be(100);
        outcome.TutorStrike.Should().BeFalse();
    }

    [Fact]
    public void Tutor_no_show_refunds_in_full_and_strikes_only_in_the_reporting_window()
    {
        var start = Now.AddHours(-1);
        var b = Confirmed(start);
        b.Cancel(CancellationActors.Tutor, "x", true, Now).Error.Code.Should().Be("no_show_report_by_learner");
        b.Cancel(CancellationActors.Learner, "x", true, start.AddMinutes(5)).Error.Code.Should().Be("no_show_too_early");
        b.Cancel(CancellationActors.Learner, "x", true, start.AddDays(3)).Error.Code.Should().Be("no_show_window_closed");
        var outcome = b.Cancel(CancellationActors.Learner, "tutor never joined", true, Now).Value;
        outcome.Should().Be(new CancellationOutcome(100, true));
        b.Status.Should().Be(BookingStatus.NoShow);
    }

    [Fact]
    public void Started_sessions_cannot_be_cancelled_and_cancelled_ones_not_again()
    {
        var b = Confirmed(Now.AddMinutes(-5));
        b.Cancel(CancellationActors.Learner, "x", false, Now).Error.Code.Should().Be("booking_already_started");
        var c = Confirmed(Now.AddDays(2));
        c.Cancel(CancellationActors.Learner, "x", false, Now).IsSuccess.Should().BeTrue();
        c.Cancel(CancellationActors.Learner, "x", false, Now).Error.Code.Should().Be("booking_not_cancellable");
    }

    [Fact]
    public void Unpaid_booking_cancels_without_refund()
    {
        var b = B.Create(Tenants.Marketplace, Tutor, TutorUser, Learner, "one_to_one", Now.AddDays(1), 60, Money.NairaAmount(8_000), null, null, Now).Value;
        var outcome = b.Cancel(CancellationActors.Learner, "abandoned", false, Now).Value;
        outcome.RefundPercent.Should().Be(100, "policy percentage");
        b.RefundPercent.Should().Be(0, "nothing was paid");
    }

    [Fact]
    public void Reschedule_rules()
    {
        var b = Confirmed(Now.AddHours(30));
        b.Reschedule(CancellationActors.Learner, Now.AddHours(48), Now).IsSuccess.Should().BeTrue();
        b.RescheduledFrom.Should().Be(Now.AddHours(30));
        b.SlotStart.Should().Be(Now.AddHours(48));
        b.SlotEnd.Should().Be(Now.AddHours(49));
        b.SlotKey.Should().Be(B.KeyFor(Tutor, Now.AddHours(48)));

        var late = Confirmed(Now.AddHours(5));
        late.Reschedule(CancellationActors.Learner, Now.AddDays(2), Now).Error.Code.Should().Be("reschedule_notice");
        late.Reschedule(CancellationActors.Tutor, Now.AddDays(2), Now).IsSuccess.Should().BeTrue("tutors may move any time before the start");
        late.Reschedule(CancellationActors.Tutor, Now.AddMinutes(10), Now).Error.Code.Should().Be("slot_too_soon");
    }

    [Fact]
    public void Completion_holds_and_release_follow_the_72_hour_rule()
    {
        var b = Confirmed(Now.AddHours(1));
        b.Complete(Now).Should().BeFalse("not finished yet");
        b.Complete(Now.AddHours(3)).Should().BeTrue();
        b.Status.Should().Be(BookingStatus.Completed);

        var hold = EarningHold.Create(Tenants.Marketplace, b.Id, TutorUser, b.Price, b.SlotEnd);
        hold.ReleasesAt.Should().Be(b.SlotEnd.AddHours(72));
        hold.Release(b.SlotEnd.AddHours(71)).Should().BeFalse();
        hold.Release(b.SlotEnd.AddHours(72)).Should().BeTrue();
        hold.Release(b.SlotEnd.AddHours(73)).Should().BeFalse("released once");
    }

    [Fact]
    public void Packages_price_deduct_and_return_sessions()
    {
        SessionPackage.Create(Tenants.Marketplace, Tutor, TutorUser, Learner, "one_to_one", 60, 7, Money.NairaAmount(8_000)).Error.Code.Should().Be("package_size_invalid");
        var five = SessionPackage.Create(Tenants.Marketplace, Tutor, TutorUser, Learner, "one_to_one", 60, 5, Money.NairaAmount(8_000)).Value;
        five.Total.Should().Be(Money.NairaAmount(40_000));
        var ten = SessionPackage.Create(Tenants.Marketplace, Tutor, TutorUser, Learner, "one_to_one", 60, 10, Money.NairaAmount(8_000)).Value;
        ten.Total.Should().Be(Money.NairaAmount(72_000), "10% off the ten-pack");
        ten.EffectivePricePerSession.Should().Be(Money.NairaAmount(7_200));

        ten.UseSession().Error.Code.Should().Be("package_exhausted", "not paid yet");
        ten.Activate(Now).Should().BeTrue();
        for (var i = 0; i < 10; i++)
        {
            ten.UseSession().IsSuccess.Should().BeTrue();
        }

        ten.Status.Should().Be(PackageStatus.Exhausted);
        ten.UseSession().Error.Code.Should().Be("package_exhausted");
        ten.ReturnSession();
        ten.Status.Should().Be(PackageStatus.Active);
        ten.SessionsRemaining.Should().Be(1);
    }

    [Fact]
    public void Three_strikes_in_90_days_is_the_review_threshold()
    {
        TutorStrike.ReviewThreshold.Should().Be(3);
        TutorStrike.Window.Should().Be(TimeSpan.FromDays(90));
    }
}
