using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Tutors.Domain;

public enum TutorLevel
{
    New = 0,
    Rising = 1,
    Top = 2,
}

public enum TutorStatus
{
    Applied = 0,
    Verified = 1,
    Suspended = 2,
    Rejected = 3,
}

public static class TeachingFormats
{
    public const string OneToOne = "one_to_one";
    public const string Group = "group";
    public const string Course = "course";
    public static readonly IReadOnlyCollection<string> All = [OneToOne, Group, Course];
    public static bool IsValid(string format) => All.Contains(format, StringComparer.Ordinal);
}

/// <summary>
/// Blueprint §13: a tutor is listed only after identity (KYC), qualifications (credential review) and subject
/// competence (demo lesson review) all pass. Levels: New → Rising (10 sessions or 20 enrolments, rating ≥ 4.5) → Top.
/// </summary>
[Audited]
public sealed class TutorProfile : Entity
{
    private TutorProfile()
    {
    }

    private TutorProfile(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid UserId { get; private set; }
    public string Headline { get; private set; } = string.Empty;
    public string Bio { get; private set; } = string.Empty;
    public List<string> Subjects { get; private set; } = [];
    public List<string> Languages { get; private set; } = [];
    public List<string> Formats { get; private set; } = [];
    public string TimeZone { get; private set; } = "Africa/Lagos";
    public Money HourlyRate { get; private set; }
    public TutorLevel Level { get; private set; }
    public TutorStatus Status { get; private set; }
    public decimal QualityScore { get; private set; }
    public int SessionsDelivered { get; private set; }
    public int EnrolmentsTaught { get; private set; }
    public decimal RatingAverage { get; private set; }
    public int RatingCount { get; private set; }
    public DateTime? CodeOfConductAcceptedAt { get; private set; }
    public DateTime? IdentityVerifiedAt { get; private set; }
    public string? VerifiedFullName { get; private set; }
    public DateTime? QualificationsReviewedAt { get; private set; }
    public string? DemoLessonBlobKey { get; private set; }
    public DateTime? DemoLessonSubmittedAt { get; private set; }
    public DateTime? SubjectReviewedAt { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public bool CalendarConnected { get; private set; }
    public string? CalendarAccount { get; private set; }

    public static Result<TutorProfile> Apply(
        Guid tenantId,
        Guid userId,
        string headline,
        string bio,
        IEnumerable<string> subjects,
        IEnumerable<string> languages,
        IEnumerable<string> formats,
        string timeZone,
        Money hourlyRate,
        bool acceptedCodeOfConduct,
        DateTime utcNow)
    {
        if (!acceptedCodeOfConduct)
        {
            return Error.Validation("code_of_conduct_required", "You must accept the tutor code of conduct.");
        }

        if (hourlyRate.IsNegative || hourlyRate.Currency != Money.Naira)
        {
            return Error.Validation("hourly_rate_invalid", "Hourly rate must be a non-negative naira amount.");
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _))
        {
            return Error.Validation("time_zone_invalid", "Unknown IANA time zone.");
        }

        return new TutorProfile(NewId(), tenantId)
        {
            UserId = userId,
            Headline = headline.Trim(),
            Bio = bio.Trim(),
            Subjects = Normalise(subjects),
            Languages = Normalise(languages),
            Formats = Normalise(formats),
            TimeZone = timeZone,
            HourlyRate = hourlyRate,
            Level = TutorLevel.New,
            Status = TutorStatus.Applied,
            CodeOfConductAcceptedAt = utcNow,
        };
    }

    public void RecordIdentityResult(bool passed, string? verifiedFullName, DateTime utcNow)
    {
        if (passed)
        {
            IdentityVerifiedAt = utcNow;
            VerifiedFullName = verifiedFullName;
        }
    }

    public void RecordQualificationsReviewed(DateTime utcNow) => QualificationsReviewedAt = utcNow;

    public Result SubmitDemoLesson(string blobKey, DateTime utcNow)
    {
        if (SubjectReviewedAt is not null)
        {
            return Error.Conflict("subject_already_reviewed", "Subject competence is already confirmed.");
        }

        DemoLessonBlobKey = blobKey;
        DemoLessonSubmittedAt = utcNow;
        return Result.Success();
    }

    public void RecordSubjectReviewed(DateTime utcNow) => SubjectReviewedAt = utcNow;

    public bool IdentityPassed => IdentityVerifiedAt is not null;
    public bool QualificationsPassed => QualificationsReviewedAt is not null;
    public bool SubjectPassed => SubjectReviewedAt is not null;
    public bool IsVerified => Status == TutorStatus.Verified;

    /// <summary>Flips to Verified once every stage passed. Returns true the first time it does so (caller publishes the event).</summary>
    public bool TryVerify(DateTime utcNow)
    {
        if (IsVerified || Status is TutorStatus.Suspended or TutorStatus.Rejected)
        {
            return false;
        }

        if (!IdentityPassed || !QualificationsPassed || !SubjectPassed)
        {
            return false;
        }

        Status = TutorStatus.Verified;
        VerifiedAt = utcNow;
        return true;
    }

    public void Reject() => Status = TutorStatus.Rejected;

    public Result SetPricing(Money hourlyRate)
    {
        if (hourlyRate.IsNegative || hourlyRate.Currency != Money.Naira)
        {
            return Error.Validation("hourly_rate_invalid", "Hourly rate must be a non-negative naira amount.");
        }

        HourlyRate = hourlyRate;
        return Result.Success();
    }

    public Result SetTimeZone(string timeZone)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _))
        {
            return Error.Validation("time_zone_invalid", "Unknown IANA time zone.");
        }

        TimeZone = timeZone;
        return Result.Success();
    }

    public void UpdateProfile(string? headline, string? bio, IEnumerable<string>? subjects, IEnumerable<string>? languages, IEnumerable<string>? formats)
    {
        if (!string.IsNullOrWhiteSpace(headline))
        {
            Headline = headline.Trim();
        }

        if (!string.IsNullOrWhiteSpace(bio))
        {
            Bio = bio.Trim();
        }

        if (subjects is not null)
        {
            Subjects = Normalise(subjects);
        }

        if (languages is not null)
        {
            Languages = Normalise(languages);
        }

        if (formats is not null)
        {
            Formats = Normalise(formats);
        }
    }

    public void ConnectCalendar(string account)
    {
        CalendarConnected = true;
        CalendarAccount = account;
    }

    /// <summary>Blueprint §13 level thresholds. Recomputed whenever sessions, enrolments or ratings change.</summary>
    public void RecordOutcome(int sessionsDelta, int enrolmentsDelta, decimal? newRating)
    {
        SessionsDelivered += sessionsDelta;
        EnrolmentsTaught += enrolmentsDelta;
        if (newRating is { } r)
        {
            RatingAverage = (RatingAverage * RatingCount + r) / (RatingCount + 1);
            RatingCount++;
        }

        QualityScore = Math.Round(RatingCount == 0 ? 0 : RatingAverage / 5m, 3);
        if (Level == TutorLevel.New && (SessionsDelivered >= 10 || EnrolmentsTaught >= 20) && RatingAverage >= 4.5m)
        {
            Level = TutorLevel.Rising;
        }
    }

    public static List<string> Normalise(IEnumerable<string> values)
        => values.Select(v => v.Trim().ToLowerInvariant()).Where(v => v.Length > 0).Distinct().Take(20).ToList();
}
