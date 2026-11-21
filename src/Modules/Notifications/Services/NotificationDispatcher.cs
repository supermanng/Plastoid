using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Sms;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Notifications.Domain;
using Plastoids.Modules.Notifications.Persistence;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Notifications.Services;

/// <summary>Resolves the approved template (or the built-in default), renders it, writes the inbox item and queues one external delivery by preference.</summary>
public sealed class NotificationDispatcher(NotificationsDbContext db, IUserContacts contacts, ICurrentTenant tenant, IClock clock, ILogger<NotificationDispatcher> logger)
{
    private static readonly string[] IdKeys = ["certificateId", "enrolmentId", "bookingId", "applicationId", "payoutId", "programmeId", "threadId", "announcementId", "userId"];

    /// <summary>Stable key so the same fact reaching us twice (domain event and send-requested) yields one inbox item.</summary>
    public static string DedupKeyFor(string templateKey, IReadOnlyDictionary<string, string> data, Guid fallback)
    {
        foreach (var key in IdKeys)
        {
            if (data.TryGetValue(key, out var id) && !string.IsNullOrWhiteSpace(id))
            {
                return $"{templateKey}:{key}:{id}";
            }
        }

        return $"{templateKey}:{fallback:N}";
    }

    public async Task<Notification?> DispatchAsync(Guid recipientId, string templateKey, IReadOnlyDictionary<string, string> data, string channelPreference, Guid fallbackId, CancellationToken ct)
    {
        var dedupKey = DedupKeyFor(templateKey, data, fallbackId);
        if (db.Notifications.Local.Any(n => n.DedupKey == dedupKey) || await db.Notifications.AnyAsync(n => n.DedupKey == dedupKey, ct))
        {
            return null;
        }

        var contact = await contacts.GetAsync(recipientId, ct);
        var merged = new Dictionary<string, string>(data, StringComparer.Ordinal);
        merged.TryAdd("displayName", contact?.DisplayName ?? "there");
        var now = clock.UtcNow;

        var inApp = await ResolveAsync(templateKey, Channels.InApp, merged, ct);
        var notification = Notification.Create(tenant.TenantId, recipientId, templateKey, dedupKey, inApp.Subject, inApp.Body, JsonSerializer.Serialize(merged));
        db.Notifications.Add(notification);

        if (contact is null || !contact.IsActive)
        {
            return notification;
        }

        var preference = await db.Preferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == recipientId, ct) ?? ChannelPreference.Default(tenant.TenantId, recipientId);
        var candidates = Channels.IsExternal(channelPreference) ? [channelPreference] : preference.ActiveChannels();
        foreach (var channel in candidates)
        {
            if (preference.OptedOut.Contains(channel))
            {
                continue;
            }

            var destination = channel switch
            {
                Channels.Sms or Channels.WhatsApp => contact.PhoneE164,
                Channels.Email => contact.Email,
                Channels.Push => recipientId.ToString("D"),
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(destination))
            {
                continue;
            }

            db.Deliveries.Add(Delivery.Queue(tenant.TenantId, notification, channel, destination, preference.QuietEndsAt(now)));
            break; // one external channel per message; the next in order is the fallback when this one fails
        }

        return notification;
    }

    public async Task<RenderedMessage> ResolveAsync(string templateKey, string channel, IReadOnlyDictionary<string, string> data, CancellationToken ct)
    {
        var template = await db.Templates.AsNoTracking()
            .Where(t => t.Key == templateKey && t.Approved && (t.Channel == channel || t.Channel == "any"))
            .OrderByDescending(t => t.Channel == channel).ThenByDescending(t => t.Version)
            .FirstOrDefaultAsync(ct);
        if (template is not null)
        {
            return TemplateRenderer.Render(template.Subject, template.Body, data);
        }

        if (!DefaultTemplates.All.ContainsKey(templateKey))
        {
            logger.LogWarning("No template for key {TemplateKey}; using the generic template", templateKey);
        }

        var (subject, body) = DefaultTemplates.For(templateKey);
        return TemplateRenderer.Render(subject, body, data);
    }
}

/// <summary>Sends one due delivery through its channel adapter.</summary>
public sealed class ChannelRouter(NotificationsDbContext db, NotificationDispatcher dispatcher, ISmsSender sms, IWhatsAppSender whatsApp, IEmailSender email, IPushSender push, IClock clock)
{
    public async Task SendAsync(Delivery delivery, CancellationToken ct)
    {
        var notification = await db.Notifications.AsNoTracking().SingleOrDefaultAsync(n => n.Id == delivery.NotificationId, ct);
        if (notification is null)
        {
            delivery.Skip("notification_missing");
            return;
        }

        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(notification.DataJson) ?? [];
        var rendered = await dispatcher.ResolveAsync(notification.TemplateKey, delivery.Channel, data, ct);
        var message = new OutboundMessage(delivery.RecipientId, rendered.Subject, rendered.Body, data);
        var now = clock.UtcNow;
        try
        {
            switch (delivery.Channel)
            {
                case Channels.Sms:
                    await sms.SendAsync(delivery.Destination, rendered.Body, ct);
                    delivery.MarkSent("sms_" + Guid.CreateVersion7().ToString("N"), now);
                    return;
                case Channels.WhatsApp:
                    Apply(delivery, await whatsApp.SendAsync(delivery.Destination, message, ct), now);
                    return;
                case Channels.Email:
                    Apply(delivery, await email.SendAsync(delivery.Destination, message, ct), now);
                    return;
                case Channels.Push:
                    Apply(delivery, await push.SendAsync(delivery.RecipientId, message, ct), now);
                    return;
                default:
                    delivery.Skip("channel_unknown");
                    return;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TimeoutException)
        {
            delivery.MarkFailed(ex.Message, now);
        }
    }

    private static void Apply(Delivery delivery, SharedKernel.Results.Result<string> result, DateTime now)
    {
        if (result.IsSuccess)
        {
            delivery.MarkSent(result.Value, now);
        }
        else
        {
            delivery.MarkFailed(result.Error.Code, now);
        }
    }
}
