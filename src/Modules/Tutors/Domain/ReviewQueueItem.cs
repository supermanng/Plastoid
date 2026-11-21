using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Tutors.Domain;

public static class ReviewKinds
{
    public const string Credential = "credential";
    public const string Subject = "subject";
}

/// <summary>Reviewer queue with SLA timers (blueprint §13: 3 working days for qualifications, 5 for subject competence).</summary>
public sealed class ReviewQueueItem : Entity
{
    public const int CredentialSlaWorkingDays = 3;
    public const int SubjectSlaWorkingDays = 5;

    private ReviewQueueItem()
    {
    }

    private ReviewQueueItem(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid TutorProfileId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public Guid ReferenceId { get; private set; }
    public DateTime EnqueuedAt { get; private set; }
    public DateTime DueAt { get; private set; }
    public Guid? CompletedBy { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? SlaAlertedAt { get; private set; }

    public static ReviewQueueItem Enqueue(Guid tenantId, Guid tutorProfileId, string kind, Guid referenceId, DateTime utcNow, DateTime dueAt)
        => new(NewId(), tenantId) { TutorProfileId = tutorProfileId, Kind = kind, ReferenceId = referenceId, EnqueuedAt = utcNow, DueAt = dueAt };

    public void Complete(Guid reviewerId, DateTime utcNow)
    {
        CompletedBy = reviewerId;
        CompletedAt = utcNow;
    }

    public void MarkAlerted(DateTime utcNow) => SlaAlertedAt = utcNow;

    public bool IsOverdue(DateTime utcNow) => CompletedAt is null && SlaAlertedAt is null && utcNow > DueAt;
}
