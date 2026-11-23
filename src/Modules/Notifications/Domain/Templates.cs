using System.Text.RegularExpressions;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Notifications.Domain;

public static class Channels
{
    public const string InApp = "inapp";
    public const string Push = "push";
    public const string WhatsApp = "whatsapp";
    public const string Sms = "sms";
    public const string Email = "email";
    public static readonly IReadOnlyList<string> External = [Push, WhatsApp, Sms, Email];
    public static bool IsExternal(string c) => External.Contains(c, StringComparer.Ordinal);
}

/// <summary>An approved, versioned message template: {{placeholder}} substitution (blueprint §9.16 "approved templates").</summary>
public sealed class NotificationTemplate : Entity
{
    private NotificationTemplate()
    {
    }

    private NotificationTemplate(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string Key { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public string? Subject { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public bool Approved { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }

    public static NotificationTemplate Create(Guid tenantId, string key, string channel, int version, string? subject, string body, Guid createdBy)
        => new(NewId(), tenantId) { Key = key.Trim(), Channel = channel, Version = version, Subject = subject?.Trim(), Body = body.Trim(), CreatedBy = createdBy };

    public Result Approve(Guid approvedBy, DateTime utcNow)
    {
        if (Approved)
        {
            return Error.Conflict("template_already_approved", "This version is already approved.");
        }

        Approved = true;
        ApprovedBy = approvedBy;
        ApprovedAt = utcNow;
        return Result.Success();
    }
}

public sealed record RenderedMessage(string? Subject, string Body);

/// <summary>{{key}} substitution; unknown placeholders render empty so a missing field never leaks the template syntax.</summary>
public static partial class TemplateRenderer
{
    public static RenderedMessage Render(string? subject, string body, IReadOnlyDictionary<string, string> data)
        => new(subject is null ? null : Substitute(subject, data), Substitute(body, data));

    public static string Substitute(string text, IReadOnlyDictionary<string, string> data)
        => Placeholder().Replace(text, m => data.TryGetValue(m.Groups[1].Value, out var v) ? v : string.Empty);

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_.]+)\s*\}\}")]
    private static partial Regex Placeholder();
}

/// <summary>Built-in copy for every template key the modules emit; the templates table overrides these once a version is approved.</summary>
public static class DefaultTemplates
{
    public const string GenericKey = "generic";

    public static readonly IReadOnlyDictionary<string, (string Subject, string Body)> All = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        [GenericKey] = ("Plastoids update", "You have a new update on Plastoids. Open the app to see it."),
        ["identity.user.registered"] = ("Welcome to Plastoids", "Welcome {{displayName}}! Your Plastoids account is ready. Browse courses and sponsored programmes to get started."),
        ["booking.confirmed.learner"] = ("Session confirmed", "Your session on {{slotStart}} is confirmed. Join from the app 15 minutes before it starts."),
        ["booking.confirmed.tutor"] = ("New session booked", "A learner booked you for {{slotStart}}. It is on your calendar."),
        ["booking.cancelled.learner"] = ("Session cancelled", "Your session on {{slotStart}} was cancelled. Refund: {{refundPercent}}% as credit."),
        ["booking.cancelled.tutor"] = ("Session cancelled", "The session on {{slotStart}} was cancelled."),
        ["booking.rescheduled.learner"] = ("Session moved", "Your session now starts at {{slotStart}}."),
        ["booking.rescheduled.tutor"] = ("Session moved", "The session now starts at {{slotStart}}."),
        ["lms.enrolment.created"] = ("You are enrolled", "You are enrolled in {{courseTitle}}. Start learning in the app."),
        ["lms.enrolment.completed"] = ("Course completed", "Congratulations! You completed {{courseTitle}} with {{finalScore}}%. Your certificate is on its way."),
        ["lms.assignment.graded"] = ("Assignment graded", "Your assignment was graded: {{score}}."),
        ["certificates.issued"] = ("Your certificate is ready", "Your certificate for {{courseTitle}} is ready. Verification code: {{publicCode}}."),
        ["applications.application.submitted"] = ("Application received", "We received your application for {{programme}}. {{hasAssessment}}"),
        ["applications.decision.accepted"] = ("Application accepted", "Good news: your application was accepted. Watch for your offer."),
        ["applications.decision.rejected"] = ("Application decision", "Your application was not successful this time. You can appeal within 7 days."),
        ["applications.decision.waitlisted"] = ("You are on the waitlist", "You are on the waitlist; we will tell you as soon as a seat opens."),
        ["applications.offer.sent"] = ("You have an offer", "{{sponsor}} is offering you a seat on {{programme}}. Accept before {{expiresAt}}. Co-pay: {{coPay}}."),
        ["applications.offer.accepted"] = ("Seat confirmed", "Your seat on {{programme}} is confirmed. See you in class!"),
        ["applications.offer.expired"] = ("Offer expired", "Your offer expired. You are back on the waitlist if seats open."),
        ["applications.appeal.upheld"] = ("Appeal upheld", "Your appeal was upheld: {{reason}}. You are back in the pool."),
        ["applications.appeal.dismissed"] = ("Appeal decision", "Your appeal was not upheld: {{reason}}."),
        ["sponsorship.quote.approved"] = ("Quote approved", "Invoice {{invoiceNumber}} for {{total}} is ready. Pay to virtual account {{virtualAccount}}."),
        ["sponsorship.programme.reviewed"] = ("Programme review", "Plastoids reviewed your programme: {{decision}}. {{notes}}"),
        ["sponsorship.programme.funded"] = ("Programme funded", "{{name}} is funded: {{seats}} seats are unlocked."),
        ["sponsorship.programme.closed"] = ("Programme closed", "Your programme is closed. Refund of unfilled seats: {{refund}}; remaining funds {{remaining}} ({{policy}})."),
        ["sponsorship.seat.allocated"] = ("You have a sponsored seat", "{{sponsorName}} has given you a seat in {{cohortName}} ({{courseTitle}}). It is in My courses."),
        ["sponsorship.seat.invited"] = ("A seat is waiting for you", "{{sponsorName}} reserved a seat for you in {{cohortName}}. Sign up with this phone number or email to take it."),
        ["catalogue.engagement.offered"] = ("Teaching offer", "{{organisationName}} would like you to teach {{courseTitle}}. Review the terms in Offers."),
        ["catalogue.engagement.accepted"] = ("Tutor accepted", "Your teaching offer for {{courseTitle}} was accepted."),
        ["catalogue.engagement.declined"] = ("Tutor declined", "Your teaching offer for {{courseTitle}} was declined."),
        ["catalogue.engagement.ended"] = ("Engagement ended", "{{organisationName}} ended your engagement on {{courseTitle}}."),
        ["payouts.payout.settled"] = ("Payout sent", "{{amount}} was paid to your bank account (ref {{bankRef}})."),
        ["payouts.payout.failed"] = ("Payout failed", "Your payout of {{amount}} failed: {{reason}}. The money is back on your balance."),
        ["payouts.payout.reversed"] = ("Payout reversed", "Your payout of {{amount}} was reversed by the bank: {{reason}}. The money is back on your balance."),
        ["payouts.hold.placed"] = ("Payout on hold", "Your payouts are on hold: {{reason}}. Released by {{releasesAt}} at the latest."),
        ["payouts.hold.released"] = ("Payout hold released", "The hold on your payouts was released: {{resolution}}."),
        ["notifications.cohort.announcement"] = ("{{title}}", "{{body}}"),
        ["notifications.thread.message"] = ("New message", "{{senderName}}: {{preview}}"),
    };

    public static (string Subject, string Body) For(string key) => All.TryGetValue(key, out var t) ? t : All[GenericKey];
}
