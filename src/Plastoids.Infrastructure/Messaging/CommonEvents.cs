using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Infrastructure.Messaging;

/// <summary>
/// notifications.send-requested.v1 (build prompt §2.8): any module asks Notifications to deliver a templated message.
/// Lives in Infrastructure, like audit.event.v1, because every module produces it. Channel preference: "preferred" lets
/// Notifications pick from the user's settings; otherwise whatsapp | sms | email | push | in_app.
/// </summary>
[IntegrationEvent("notifications", "send-requested", 1)]
public sealed record NotificationRequested(
    Guid AggregateId,
    string TemplateKey,
    Guid RecipientId,
    string ChannelPreference,
    IReadOnlyDictionary<string, string> Data) : IIntegrationEvent
{
    public static NotificationRequested To(Guid recipientId, string templateKey, IReadOnlyDictionary<string, string> data, string channelPreference = "preferred")
        => new(recipientId, templateKey, recipientId, channelPreference, data);
}
