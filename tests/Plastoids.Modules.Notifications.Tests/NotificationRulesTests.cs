using Plastoids.Modules.Notifications.Domain;
using Plastoids.Modules.Notifications.Services;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Notifications.Tests;

public class NotificationRulesTests
{
    [Fact]
    public void Templates_render_placeholders_and_fall_back_to_generic()
    {
        var data = new Dictionary<string, string> { ["courseTitle"] = "SQL for Analysts", ["publicCode"] = "AB12-CD34-EF56" };
        var (subject, body) = DefaultTemplates.For("certificates.issued");
        var rendered = TemplateRenderer.Render(subject, body, data);
        rendered.Subject.Should().Be("Your certificate is ready");
        rendered.Body.Should().Be("Your certificate for SQL for Analysts is ready. Verification code: AB12-CD34-EF56.");

        TemplateRenderer.Substitute("Hi {{ displayName }}, {{missing}} done", new Dictionary<string, string> { ["displayName"] = "Ada" }).Should().Be("Hi Ada,  done");
        DefaultTemplates.For("no.such.key").Should().Be(DefaultTemplates.All[DefaultTemplates.GenericKey]);

        var payout = TemplateRenderer.Render(DefaultTemplates.For("payouts.payout.settled").Subject, DefaultTemplates.For("payouts.payout.settled").Body, new Dictionary<string, string> { ["amount"] = "NGN 36000.00", ["bankRef"] = "NIP123" });
        payout.Body.Should().Be("NGN 36000.00 was paid to your bank account (ref NIP123).");
    }

    [Fact]
    public void Sponsored_seat_templates_render_the_sponsor_and_cohort()
    {
        var data = new Dictionary<string, string> { ["sponsorName"] = "Lagos Data School", ["cohortName"] = "Jan intake", ["courseTitle"] = "SQL for Analysts" };
        var (subject, body) = DefaultTemplates.For("sponsorship.seat.allocated");
        var allocated = TemplateRenderer.Render(subject, body, data);
        allocated.Subject.Should().Be("You have a sponsored seat");
        allocated.Body.Should().Be("Lagos Data School has given you a seat in Jan intake (SQL for Analysts). It is in My courses.");

        var (invitedSubject, invitedBody) = DefaultTemplates.For("sponsorship.seat.invited");
        var invited = TemplateRenderer.Render(invitedSubject, invitedBody, data);
        invited.Subject.Should().Be("A seat is waiting for you");
        invited.Body.Should().Be("Lagos Data School reserved a seat for you in Jan intake. Sign up with this phone number or email to take it.");
    }

    [Fact]
    public void Every_template_key_the_modules_emit_has_copy()
    {
        string[] emitted =
        [
            "applications.application.submitted", "applications.decision.accepted", "applications.decision.rejected", "applications.decision.waitlisted", "applications.appeal.upheld", "applications.appeal.dismissed",
            "applications.offer.sent", "applications.offer.accepted", "applications.offer.expired", "booking.confirmed.learner", "booking.confirmed.tutor", "booking.cancelled.learner", "booking.cancelled.tutor",
            "booking.rescheduled.learner", "booking.rescheduled.tutor", "certificates.issued", "lms.assignment.graded", "lms.enrolment.completed", "lms.enrolment.created", "payouts.hold.placed", "payouts.hold.released",
            "payouts.payout.failed", "payouts.payout.reversed", "payouts.payout.settled", "sponsorship.programme.closed", "sponsorship.programme.funded", "sponsorship.programme.reviewed", "sponsorship.quote.approved",
            "sponsorship.seat.allocated", "sponsorship.seat.invited",
        ];
        emitted.Should().AllSatisfy(k => DefaultTemplates.All.Should().ContainKey(k));
    }

    [Fact]
    public void Preferences_route_by_order_and_opt_outs()
    {
        var p = ChannelPreference.Default(Tenants.Marketplace, Guid.NewGuid());
        p.ActiveChannels().Should().Equal(Channels.Push, Channels.WhatsApp, Channels.Sms, Channels.Email);
        p.Update(["email", "sms"], ["sms"], null, null, null).IsSuccess.Should().BeTrue();
        p.ActiveChannels().Should().Equal(Channels.Email);
        p.Update(["inapp"], null, null, null, null).Error.Code.Should().Be("channel_invalid");
        p.Update(null, null, new TimeOnly(22, 0), null, null).Error.Code.Should().Be("quiet_hours_invalid");
        p.Update(null, null, null, null, "Mars/Olympus").Error.Code.Should().Be("time_zone_invalid");
    }

    [Fact]
    public void Quiet_hours_defer_until_the_window_ends_in_lagos_time()
    {
        // Lagos is UTC+1 all year. 21:00–07:00 local.
        var start = new TimeOnly(21, 0);
        var end = new TimeOnly(7, 0);
        var lateEvening = new DateTime(2026, 11, 21, 21, 30, 0, DateTimeKind.Utc); // 22:30 Lagos
        QuietWindow.IsQuiet(start, end, "Africa/Lagos", lateEvening).Should().BeTrue();
        QuietWindow.EndsAt(start, end, "Africa/Lagos", lateEvening).Should().Be(new DateTime(2026, 11, 22, 6, 0, 0, DateTimeKind.Utc));
        var earlyMorning = new DateTime(2026, 11, 22, 4, 0, 0, DateTimeKind.Utc); // 05:00 Lagos
        QuietWindow.EndsAt(start, end, "Africa/Lagos", earlyMorning).Should().Be(new DateTime(2026, 11, 22, 6, 0, 0, DateTimeKind.Utc));
        var midday = new DateTime(2026, 11, 22, 11, 0, 0, DateTimeKind.Utc);
        QuietWindow.IsQuiet(start, end, "Africa/Lagos", midday).Should().BeFalse();
        QuietWindow.IsQuiet(new TimeOnly(12, 0), new TimeOnly(14, 0), "Africa/Lagos", midday).Should().BeTrue("12:00 Lagos is inside a same-day window");
        QuietWindow.IsQuiet(null, null, "Africa/Lagos", midday).Should().BeFalse();

        var p = ChannelPreference.Default(Tenants.Marketplace, Guid.NewGuid());
        p.Update(null, null, start, end, null);
        var n = Notification.Create(Tenants.Marketplace, p.UserId, "x", "x:1", null, "body", "{}");
        var delivery = Delivery.Queue(Tenants.Marketplace, n, Channels.Sms, "+2348030000000", p.QuietEndsAt(lateEvening));
        delivery.Status.Should().Be(DeliveryStatus.Deferred);
        delivery.IsDue(lateEvening).Should().BeFalse();
        delivery.IsDue(new DateTime(2026, 11, 22, 6, 1, 0, DateTimeKind.Utc)).Should().BeTrue();
    }

    [Fact]
    public void Failed_deliveries_retry_then_fail()
    {
        var n = Notification.Create(Tenants.Marketplace, Guid.NewGuid(), "x", "x:2", null, "body", "{}");
        var d = Delivery.Queue(Tenants.Marketplace, n, Channels.Email, "a@b.c", null);
        var now = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
        d.MarkFailed("smtp down", now);
        d.Status.Should().Be(DeliveryStatus.Deferred);
        d.DeferredUntil.Should().Be(now.AddMinutes(5));
        d.MarkFailed("smtp down", now);
        d.MarkFailed("smtp down", now);
        d.Status.Should().Be(DeliveryStatus.Failed);
        d.Attempts.Should().Be(3);
    }

    [Fact]
    public void Thread_messages_are_scanned_and_flagged_not_dropped()
    {
        var now = DateTime.UtcNow;
        var learner = Guid.NewGuid();
        var tutor = Guid.NewGuid();
        var thread = MessageThread.Open(Tenants.Marketplace, learner, tutor, "Homework", null, now);
        var ok = thread.Post(learner, "Can we review question 3 tomorrow?", now);
        ok.IsFlagged.Should().BeFalse();
        var phone = thread.Post(tutor, "Call me on 08031234567", now);
        phone.FlagReason.Should().Be("contact_details");
        var payment = thread.Post(tutor, "Just pay me directly and skip the platform", now);
        payment.FlagReason.Should().Be("off_platform_payment");
        thread.IsFlagged.Should().BeTrue();
        thread.MessageCount.Should().Be(3);
        thread.OtherParty(learner).Should().Be(tutor);
        Moderation.Scan("whatsapp me for the notes").Should().Be("off_platform_payment");
    }

    [Fact]
    public void Dedup_key_prefers_a_stable_id_so_event_and_send_requested_collapse()
    {
        var fromEvent = NotificationDispatcher.DedupKeyFor("certificates.issued", new Dictionary<string, string> { ["certificateId"] = "abc", ["publicCode"] = "X" }, Guid.NewGuid());
        var fromRequest = NotificationDispatcher.DedupKeyFor("certificates.issued", new Dictionary<string, string> { ["publicCode"] = "X", ["certificateId"] = "abc", ["courseTitle"] = "T" }, Guid.NewGuid());
        fromEvent.Should().Be(fromRequest).And.Be("certificates.issued:certificateId:abc");
        var fallback = Guid.NewGuid();
        NotificationDispatcher.DedupKeyFor("generic", new Dictionary<string, string>(), fallback).Should().Be($"generic:{fallback:N}");
    }
}
