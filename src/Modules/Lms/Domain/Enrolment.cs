using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Lms.Domain;

public enum EnrolmentStatus
{
    Active = 0,
    Completed = 1,
    Withdrawn = 2,
}

/// <summary>Blueprint §16 lms.Enrolment: one learner on one offering, with the funding source and the course version they study.</summary>
[Audited]
public sealed class Enrolment : Entity
{
    private Enrolment()
    {
    }

    private Enrolment(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid LearnerId { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid OfferingId { get; private set; }
    public Guid? CohortId { get; private set; }
    public int VersionNo { get; private set; }
    public string FundingSource { get; private set; } = string.Empty;
    public Guid? FundingSourceId { get; private set; }

    /// <summary>Idempotency key for enrolment creation: the payment intent or application that produced it.</summary>
    public string SourceKey { get; private set; } = string.Empty;

    public EnrolmentStatus Status { get; private set; }
    public decimal ProgressPct { get; private set; }
    public int RequiredLessons { get; private set; }
    public int CompletedLessons { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public decimal? FinalScore { get; private set; }

    public static Enrolment Create(Guid tenantId, Guid learnerId, Guid courseId, Guid offeringId, Guid? cohortId, int versionNo, string fundingSource, Guid? fundingSourceId, string sourceKey, int requiredLessons, DateTime utcNow, DateTime expiresAt)
        => new(NewId(), tenantId)
        {
            LearnerId = learnerId,
            CourseId = courseId,
            OfferingId = offeringId,
            CohortId = cohortId,
            VersionNo = versionNo,
            FundingSource = fundingSource,
            FundingSourceId = fundingSourceId,
            SourceKey = sourceKey,
            Status = EnrolmentStatus.Active,
            RequiredLessons = requiredLessons,
            StartedAt = utcNow,
            ExpiresAt = expiresAt,
        };

    public void UpdateProgress(int completedLessons)
    {
        CompletedLessons = Math.Min(completedLessons, Math.Max(RequiredLessons, completedLessons));
        ProgressPct = RequiredLessons == 0 ? 0 : Math.Round(Math.Min(100m, 100m * CompletedLessons / RequiredLessons), 2);
    }

    /// <summary>Completion rule (story E4): every required lesson complete and every pass mark met. Returns true the first time.</summary>
    public bool TryComplete(bool allPassMarksMet, decimal finalScore, DateTime utcNow)
    {
        if (Status != EnrolmentStatus.Active || CompletedLessons < RequiredLessons || RequiredLessons == 0 || !allPassMarksMet)
        {
            return false;
        }

        Status = EnrolmentStatus.Completed;
        CompletedAt = utcNow;
        FinalScore = Math.Round(finalScore, 2);
        ProgressPct = 100m;
        return true;
    }

    public Result Withdraw()
    {
        if (Status != EnrolmentStatus.Active)
        {
            return Error.Conflict("enrolment_not_active", "Only active enrolments can be withdrawn.");
        }

        Status = EnrolmentStatus.Withdrawn;
        return Result.Success();
    }

    public bool IsActive => Status == EnrolmentStatus.Active;
}

public enum LessonStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
}

/// <summary>
/// Per-lesson progress. Offline clients send batches with their own timestamps; the merge keeps the newer record per
/// lesson and completion never regresses (blueprint §12: quizzes/lessons work offline and sync later).
/// </summary>
public sealed class LessonProgress : Entity
{
    private LessonProgress()
    {
    }

    private LessonProgress(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid EnrolmentId { get; private set; }
    public Guid LessonId { get; private set; }
    public LessonStatus Status { get; private set; }
    public int PositionSeconds { get; private set; }
    public int Page { get; private set; }
    public decimal PercentComplete { get; private set; }
    public DateTime ClientUpdatedAt { get; private set; }
    public DateTime LastSyncedAt { get; private set; }

    public static LessonProgress Start(Guid tenantId, Guid enrolmentId, Guid lessonId, DateTime utcNow)
        => new(NewId(), tenantId) { EnrolmentId = enrolmentId, LessonId = lessonId, Status = LessonStatus.NotStarted, ClientUpdatedAt = DateTime.MinValue, LastSyncedAt = utcNow };

    /// <summary>Returns true when the record changed.</summary>
    public bool Merge(LessonStatus status, int positionSeconds, int page, decimal percentComplete, DateTime clientUpdatedAt, DateTime utcNow)
    {
        var wasCompleted = Status == LessonStatus.Completed;
        if (clientUpdatedAt < ClientUpdatedAt && !(status == LessonStatus.Completed && !wasCompleted))
        {
            return false; // stale offline record, and it does not add a completion
        }

        Status = wasCompleted ? LessonStatus.Completed : status;
        PositionSeconds = Math.Max(0, positionSeconds);
        Page = Math.Max(0, page);
        PercentComplete = wasCompleted || status == LessonStatus.Completed ? 100m : Math.Clamp(percentComplete, 0m, 100m);
        ClientUpdatedAt = clientUpdatedAt > ClientUpdatedAt ? clientUpdatedAt : ClientUpdatedAt;
        LastSyncedAt = utcNow;
        return true;
    }

    public void MarkCompleted(DateTime utcNow)
    {
        Status = LessonStatus.Completed;
        PercentComplete = 100m;
        LastSyncedAt = utcNow;
        ClientUpdatedAt = utcNow > ClientUpdatedAt ? utcNow : ClientUpdatedAt;
    }
}
