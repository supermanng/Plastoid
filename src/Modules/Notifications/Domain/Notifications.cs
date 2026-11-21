using System.Text.RegularExpressions;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Notifications.Domain;

/// <summary>In-app inbox item (blueprint §9.16). Deliveries on external channels hang off it.</summary>
public sealed class Notification : Entity
{
    private Notification()
    {
    }

    private Notification(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid RecipientId { get; private set; }
    public string TemplateKey { get; private set; } = string.Empty;
    public string DedupKey { get; private set; } = string.Empty;
    public string? Subject { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public string DataJson { get; private set; } = "{}";
    public DateTime? ReadAt { get; private set; }

    public static Notification Create(Guid tenantId, Guid recipientId, string templateKey, string dedupKey, string? subject, string body, string dataJson)
        => new(NewId(), tenantId) { RecipientId = recipientId, TemplateKey = templateKey, DedupKey = dedupKey, Subject = subject, Body = body, DataJson = dataJson };

    public void MarkRead(DateTime utcNow) => ReadAt ??= utcNow;
}

public enum DeliveryStatus
{
    Queued = 0,
    Deferred = 1,
    Sent = 2,
    Failed = 3,
    Skipped = 4,
}

/// <summary>One attempt to reach the recipient on one external channel.</summary>
public sealed class Delivery : Entity
{
    public const int MaxAttempts = 3;

    private Delivery()
    {
    }

    private Delivery(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid NotificationId { get; private set; }
    public Guid RecipientId { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public string Destination { get; private set; } = string.Empty;
    public DeliveryStatus Status { get; private set; }
    public DateTime? DeferredUntil { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? ProviderReference { get; private set; }
    public string? Error { get; private set; }
    public int Attempts { get; private set; }

    public static Delivery Queue(Guid tenantId, Notification notification, string channel, string destination, DateTime? deferredUntil)
        => new(NewId(), tenantId) { NotificationId = notification.Id, RecipientId = notification.RecipientId, Channel = channel, Destination = destination, Status = deferredUntil is null ? DeliveryStatus.Queued : DeliveryStatus.Deferred, DeferredUntil = deferredUntil };

    public bool IsDue(DateTime utcNow) => Status == DeliveryStatus.Queued || (Status == DeliveryStatus.Deferred && DeferredUntil <= utcNow);

    public void Defer(DateTime until)
    {
        Status = DeliveryStatus.Deferred;
        DeferredUntil = until;
    }

    public void MarkSent(string providerReference, DateTime utcNow)
    {
        Status = DeliveryStatus.Sent;
        ProviderReference = providerReference;
        SentAt = utcNow;
        Attempts++;
    }

    public void MarkFailed(string error, DateTime utcNow)
    {
        Attempts++;
        Error = error;
        if (Attempts >= MaxAttempts)
        {
            Status = DeliveryStatus.Failed;
        }
        else
        {
            Status = DeliveryStatus.Deferred;
            DeferredUntil = utcNow.AddMinutes(5 * Attempts);
        }
    }

    public void Skip(string reason)
    {
        Status = DeliveryStatus.Skipped;
        Error = reason;
    }
}

/// <summary>Learner–tutor conversation, kept on platform and retained (blueprint §19).</summary>
public sealed class MessageThread : Entity
{
    private MessageThread()
    {
    }

    private MessageThread(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid LearnerUserId { get; private set; }
    public Guid TutorUserId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public Guid? ContextId { get; private set; }
    public DateTime LastMessageAt { get; private set; }
    public int MessageCount { get; private set; }
    public bool IsFlagged { get; private set; }

    public static MessageThread Open(Guid tenantId, Guid learnerUserId, Guid tutorUserId, string subject, Guid? contextId, DateTime utcNow)
        => new(NewId(), tenantId) { LearnerUserId = learnerUserId, TutorUserId = tutorUserId, Subject = subject.Trim(), ContextId = contextId, LastMessageAt = utcNow };

    public bool IsParticipant(Guid userId) => userId == LearnerUserId || userId == TutorUserId;

    public Guid OtherParty(Guid userId) => userId == LearnerUserId ? TutorUserId : LearnerUserId;

    public ThreadMessage Post(Guid authorUserId, string body, DateTime utcNow)
    {
        var message = ThreadMessage.Create(TenantId, Id, authorUserId, body, utcNow);
        MessageCount++;
        LastMessageAt = utcNow;
        if (message.IsFlagged)
        {
            IsFlagged = true;
        }

        return message;
    }
}

public sealed class ThreadMessage : Entity
{
    private ThreadMessage()
    {
    }

    private ThreadMessage(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid ThreadId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTime SentAt { get; private set; }
    public string? FlagReason { get; private set; }
    public bool IsFlagged => FlagReason is not null;

    public static ThreadMessage Create(Guid tenantId, Guid threadId, Guid authorUserId, string body, DateTime utcNow)
        => new(NewId(), tenantId) { ThreadId = threadId, AuthorUserId = authorUserId, Body = body.Trim(), SentAt = utcNow, FlagReason = Moderation.Scan(body) };
}

/// <summary>Cohort-wide announcement by the tutor; fans out as notifications.</summary>
public sealed class Announcement : Entity
{
    private Announcement()
    {
    }

    private Announcement(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid CohortId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public int Recipients { get; private set; }
    public DateTime PostedAt { get; private set; }

    public static Announcement Post(Guid tenantId, Guid cohortId, Guid authorUserId, string title, string body, int recipients, DateTime utcNow)
        => new(NewId(), tenantId) { CohortId = cohortId, AuthorUserId = authorUserId, Title = title.Trim(), Body = body.Trim(), Recipients = recipients, PostedAt = utcNow };
}

/// <summary>Same screen as Lms forums (blueprint §19): phone numbers and off-platform payment solicitation flag the message.</summary>
public static partial class Moderation
{
    public static string? Scan(string text)
    {
        if (PhonePattern().IsMatch(text))
        {
            return "contact_details";
        }

        if (OffPlatformPattern().IsMatch(text))
        {
            return "off_platform_payment";
        }

        return null;
    }

    [GeneratedRegex(@"(\+?234|0)[789][01]\d{8}")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"\b(pay(?:ment)?\s+(?:me\s+)?(?:directly|outside|off[-\s]?platform)|bank\s+transfer\s+to\s+me|my\s+account\s+number|whatsapp\s+me|send\s+(?:the\s+)?money\s+to)\b", RegexOptions.IgnoreCase)]
    private static partial Regex OffPlatformPattern();
}
