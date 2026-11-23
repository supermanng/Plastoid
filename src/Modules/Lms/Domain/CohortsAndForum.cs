using System.Text.RegularExpressions;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Lms.Domain;

/// <summary>A dated run of a cohort offering with its own calendar (blueprint §12: sponsored/corporate cohorts).</summary>
[Audited]
public sealed class Cohort : Entity
{
    private Cohort()
    {
    }

    private Cohort(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid CourseId { get; private set; }
    public Guid OfferingId { get; private set; }
    public Guid TutorUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public int Capacity { get; private set; }
    public int Enrolled { get; private set; }

    /// <summary>Seats paid for by seat blocks (ADR 0021) but not yet allocated to a learner; they count against capacity.</summary>
    public int Reserved { get; private set; }
    public Guid? ProgrammeId { get; private set; }

    public static Result<Cohort> Create(Guid tenantId, Guid courseId, Guid offeringId, Guid tutorUserId, string name, DateTime startsAt, DateTime endsAt, int capacity, Guid? programmeId)
    {
        if (endsAt <= startsAt)
        {
            return Error.Validation("cohort_dates_invalid", "endsAt must be after startsAt.");
        }

        if (capacity <= 0)
        {
            return Error.Validation("cohort_capacity_invalid", "Capacity must be positive.");
        }

        return new Cohort(NewId(), tenantId) { CourseId = courseId, OfferingId = offeringId, TutorUserId = tutorUserId, Name = name.Trim(), StartsAt = startsAt, EndsAt = endsAt, Capacity = capacity, ProgrammeId = programmeId };
    }

    public bool HasSeats => Enrolled + Reserved < Capacity;

    public int SeatsLeft => Math.Max(0, Capacity - Enrolled - Reserved);

    public Result Admit()
    {
        if (!HasSeats)
        {
            return Error.Conflict("cohort_full", "This cohort is full.");
        }

        Enrolled++;
        return Result.Success();
    }

    /// <summary>A reserved seat becomes an enrolled one (ADR 0021): the reservation already counted against capacity, so no capacity check.</summary>
    public void AdmitReserved()
    {
        if (Reserved > 0)
        {
            Reserved--;
        }

        Enrolled++;
    }

    public void Release()
    {
        if (Enrolled > 0)
        {
            Enrolled--;
        }
    }

    /// <summary>Holds <paramref name="seats"/> for a seat block (ADR 0021): fails cohort_ended once the cohort is over and cohort_full past capacity.</summary>
    public Result Reserve(int seats, DateTime now)
    {
        if (seats <= 0)
        {
            return Error.Validation("seats_invalid", "Reserve at least one seat.");
        }

        if (EndsAt <= now)
        {
            return Error.Conflict("cohort_ended", "This cohort has ended.");
        }

        if (Enrolled + Reserved + seats > Capacity)
        {
            return Error.Conflict("cohort_full", $"Only {SeatsLeft} seat(s) left in this cohort.");
        }

        Reserved += seats;
        return Result.Success();
    }

    /// <summary>Gives back reserved seats that were never allocated; never below zero.</summary>
    public void ReleaseReserved(int seats)
    {
        if (seats <= 0)
        {
            return;
        }

        Reserved = Math.Max(0, Reserved - seats);
    }
}

public enum ThreadStatus
{
    Open = 0,
    Flagged = 1,
    Locked = 2,
}

/// <summary>Cohort/course discussion thread. Posts are scanned for off-platform payment solicitation (blueprint §9.4) and flagged for review.</summary>
public sealed class ForumThread : Entity
{
    private ForumThread()
    {
    }

    private ForumThread(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid CohortId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public ThreadStatus Status { get; private set; }
    public int PostCount { get; private set; }
    public DateTime LastActivityAt { get; private set; }

    public static ForumThread Create(Guid tenantId, Guid cohortId, Guid authorId, string title, string body, DateTime utcNow)
        => new(NewId(), tenantId) { CohortId = cohortId, AuthorId = authorId, Title = title.Trim(), Body = body.Trim(), Status = Moderation.Scan(title + " " + body) is null ? ThreadStatus.Open : ThreadStatus.Flagged, LastActivityAt = utcNow };

    public void RecordPost(DateTime utcNow)
    {
        PostCount++;
        LastActivityAt = utcNow;
    }

    public void Flag() => Status = Status == ThreadStatus.Locked ? Status : ThreadStatus.Flagged;

    public void Lock() => Status = ThreadStatus.Locked;
}

public sealed class ForumPost : Entity
{
    private ForumPost()
    {
    }

    private ForumPost(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid ThreadId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public string? FlagReason { get; private set; }
    public DateTime? FlaggedAt { get; private set; }

    public static ForumPost Create(Guid tenantId, Guid threadId, Guid authorId, string body, DateTime utcNow)
    {
        var reason = Moderation.Scan(body);
        return new ForumPost(NewId(), tenantId) { ThreadId = threadId, AuthorId = authorId, Body = body.Trim(), FlagReason = reason, FlaggedAt = reason is null ? null : utcNow };
    }

    public bool IsFlagged => FlagReason is not null;
}

/// <summary>Automated screening (blueprint §19): contact details and off-platform payment requests are flagged, never silently dropped.</summary>
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

/// <summary>Story E3: encrypted offline package tied to the account, expiring with the enrolment.</summary>
public sealed class OfflinePackage : Entity
{
    private OfflinePackage()
    {
    }

    private OfflinePackage(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid EnrolmentId { get; private set; }
    public Guid LearnerId { get; private set; }
    public string ManifestJson { get; private set; } = string.Empty;
    public string KeyCiphertext { get; private set; } = string.Empty;
    public DateTime IssuedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public int LessonCount { get; private set; }

    public static OfflinePackage Issue(Guid tenantId, Guid enrolmentId, Guid learnerId, string manifestJson, string keyCiphertext, int lessonCount, DateTime utcNow, DateTime expiresAt)
        => new(NewId(), tenantId) { EnrolmentId = enrolmentId, LearnerId = learnerId, ManifestJson = manifestJson, KeyCiphertext = keyCiphertext, LessonCount = lessonCount, IssuedAt = utcNow, ExpiresAt = expiresAt };
}

/// <summary>Projection of catalogue.course-published.v1 so enrolment never calls Catalogue to know a course exists.</summary>
public sealed class PublishedCourseRef : Entity
{
    private PublishedCourseRef()
    {
    }

    private PublishedCourseRef(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid CourseId { get; private set; }
    public int VersionNo { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public List<Guid> OfferingIds { get; private set; } = [];

    public static PublishedCourseRef Record(Guid tenantId, Guid courseId, int versionNo, Guid ownerUserId, string slug, IEnumerable<Guid> offeringIds)
        => new(NewId(), tenantId) { CourseId = courseId, VersionNo = versionNo, OwnerUserId = ownerUserId, Slug = slug, OfferingIds = offeringIds.ToList() };

    public void Update(int versionNo, IEnumerable<Guid> offeringIds)
    {
        VersionNo = versionNo;
        OfferingIds = offeringIds.ToList();
    }
}
