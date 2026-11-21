using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Applications.Domain;

public enum ApplicationStatus
{
    Draft = 0,
    Submitted = 1,
    Merged = 2,
    Accepted = 3,
    Rejected = 4,
    Waitlisted = 5,
    Offered = 6,
    PendingCoPay = 7,
    Enrolled = 8,
    Declined = 9,
    Expired = 10,
    Withdrawn = 11,
}

public static class DecisionKinds
{
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string Waitlisted = "waitlisted";
    public static bool IsValid(string d) => d is Accepted or Rejected or Waitlisted;
}

/// <summary>A learner's application to a sponsored programme (blueprint §9.13). One per learner per programme; ≤ 8 form fields; save-and-resume.</summary>
[Audited]
public sealed class Application : Entity
{
    public const int MaxFields = 8;
    public static readonly TimeSpan OfferValidity = TimeSpan.FromDays(5);
    public static readonly TimeSpan CoPayHold = TimeSpan.FromDays(7);
    public static readonly TimeSpan AppealWindow = TimeSpan.FromDays(7);

    private Application()
    {
    }

    private Application(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid ProgrammeId { get; private set; }
    public Guid LearnerId { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public string AnswersJson { get; private set; } = "{}";
    public string? NinHash { get; private set; }
    public string? PhoneHash { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public Guid? MergedIntoId { get; private set; }
    public string? FlagReason { get; private set; }
    public decimal? AssessmentScore { get; private set; }
    public bool? AssessmentPassed { get; private set; }
    public decimal? RubricScore { get; private set; }
    public decimal? TotalScore { get; private set; }
    public Guid? ScoredBy { get; private set; }
    public DateTime? ScoredAt { get; private set; }
    public string? LastDecision { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public Guid? SeatId { get; private set; }
    public DateTime? OfferSentAt { get; private set; }
    public DateTime? OfferExpiresAt { get; private set; }
    public DateTime? OfferAcceptedAt { get; private set; }
    public Guid? CoPayIntentId { get; private set; }
    public Money CoPayAmount { get; private set; } = Money.Zero();

    public static Application Start(Guid tenantId, Guid programmeId, Guid learnerId)
        => new(NewId(), tenantId) { ProgrammeId = programmeId, LearnerId = learnerId, Status = ApplicationStatus.Draft };

    public bool IsFlagged => FlagReason is not null;
    public bool HoldsSeat => Status is ApplicationStatus.Offered or ApplicationStatus.PendingCoPay or ApplicationStatus.Enrolled;

    public Result SaveAnswers(string answersJson)
    {
        if (Status != ApplicationStatus.Draft)
        {
            return Error.Conflict("application_already_submitted", "Answers cannot change after submission.");
        }

        AnswersJson = answersJson;
        return Result.Success();
    }

    public Result Submit(string? ninHash, string? phoneHash, DateTime utcNow)
    {
        if (Status == ApplicationStatus.Submitted)
        {
            return Result.Success();
        }

        if (Status != ApplicationStatus.Draft)
        {
            return Error.Conflict("application_not_draft", $"The application is {Status.ToString().ToLowerInvariant()}.");
        }

        NinHash = ninHash;
        PhoneHash = phoneHash;
        SubmittedAt = utcNow;
        Status = ApplicationStatus.Submitted;
        return Result.Success();
    }

    /// <summary>Same NIN or phone as another applicant on this programme: merged into the original and flagged for a human look.</summary>
    public void MergeInto(Guid originalApplicationId)
    {
        MergedIntoId = originalApplicationId;
        FlagReason = "duplicate_identity";
        Status = ApplicationStatus.Merged;
    }

    public void RecordAssessment(decimal score, bool passed)
    {
        AssessmentScore = score;
        AssessmentPassed = passed;
    }

    public void RecordScores(decimal? rubricScore, decimal totalScore, Guid scoredBy, DateTime utcNow)
    {
        RubricScore = rubricScore;
        TotalScore = totalScore;
        ScoredBy = scoredBy;
        ScoredAt = utcNow;
    }

    public Result Decide(string decision, DateTime utcNow)
    {
        if (!DecisionKinds.IsValid(decision))
        {
            return Error.Validation("decision_invalid", "decision must be accepted, rejected or waitlisted.");
        }

        if (Status is not (ApplicationStatus.Submitted or ApplicationStatus.Accepted or ApplicationStatus.Rejected or ApplicationStatus.Waitlisted))
        {
            return Error.Conflict("application_not_decidable", $"A decision cannot be recorded while the application is {Status.ToString().ToLowerInvariant()}.");
        }

        Status = decision switch
        {
            DecisionKinds.Accepted => ApplicationStatus.Accepted,
            DecisionKinds.Rejected => ApplicationStatus.Rejected,
            _ => ApplicationStatus.Waitlisted,
        };
        LastDecision = decision;
        DecidedAt = utcNow;
        return Result.Success();
    }

    public Result SendOffer(Guid seatId, DateTime utcNow)
    {
        if (Status != ApplicationStatus.Accepted)
        {
            return Error.Conflict("application_not_accepted", "Offers go to accepted applications only.");
        }

        SeatId = seatId;
        OfferSentAt = utcNow;
        OfferExpiresAt = utcNow + OfferValidity;
        Status = ApplicationStatus.Offered;
        return Result.Success();
    }

    public Result Accept(DateTime utcNow)
    {
        if (Status != ApplicationStatus.Offered)
        {
            return Error.Conflict("offer_not_open", "There is no open offer to accept.");
        }

        if (utcNow > OfferExpiresAt)
        {
            Status = ApplicationStatus.Expired;
            return Error.Conflict("offer_expired", "The offer expired after 5 days.");
        }

        OfferAcceptedAt = utcNow;
        Status = ApplicationStatus.Enrolled;
        return Result.Success();
    }

    /// <summary>Partial sponsorship: the seat is confirmed only when the learner's share is paid (blueprint §10 variations).</summary>
    public Result RequireCoPay(Guid paymentIntentId, Money amount, DateTime utcNow)
    {
        if (Status != ApplicationStatus.Offered)
        {
            return Error.Conflict("offer_not_open", "There is no open offer to accept.");
        }

        if (utcNow > OfferExpiresAt)
        {
            Status = ApplicationStatus.Expired;
            return Error.Conflict("offer_expired", "The offer expired after 5 days.");
        }

        OfferAcceptedAt = utcNow;
        CoPayIntentId = paymentIntentId;
        CoPayAmount = amount;
        Status = ApplicationStatus.PendingCoPay;
        return Result.Success();
    }

    public bool ConfirmCoPay()
    {
        if (Status != ApplicationStatus.PendingCoPay)
        {
            return false;
        }

        Status = ApplicationStatus.Enrolled;
        return true;
    }

    public Result Decline()
    {
        if (Status is not (ApplicationStatus.Offered or ApplicationStatus.PendingCoPay))
        {
            return Error.Conflict("offer_not_open", "There is no open offer to decline.");
        }

        Status = ApplicationStatus.Declined;
        return Result.Success();
    }

    /// <summary>Unanswered after 5 days, or unpaid co-pay after the 7-day hold.</summary>
    public bool ExpireIfDue(DateTime utcNow)
    {
        var due = Status switch
        {
            ApplicationStatus.Offered => utcNow > OfferExpiresAt,
            ApplicationStatus.PendingCoPay => utcNow > OfferAcceptedAt + CoPayHold,
            _ => false,
        };
        if (due)
        {
            Status = ApplicationStatus.Expired;
        }

        return due;
    }

    public Result Withdraw()
    {
        if (Status is ApplicationStatus.Enrolled or ApplicationStatus.Withdrawn or ApplicationStatus.Merged)
        {
            return Error.Conflict("application_not_withdrawable", $"The application is {Status.ToString().ToLowerInvariant()}.");
        }

        Status = ApplicationStatus.Withdrawn;
        return Result.Success();
    }

    public bool CanAppeal(DateTime utcNow) => Status == ApplicationStatus.Rejected && DecidedAt is { } d && utcNow <= d + AppealWindow;
}

/// <summary>Append-only record of every decision with reviewer and reason (story F4); protected by a database trigger.</summary>
public sealed class Decision : Entity
{
    private Decision()
    {
    }

    private Decision(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid ApplicationId { get; private set; }
    public Guid ProgrammeId { get; private set; }
    public Guid LearnerId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public Guid DecidedBy { get; private set; }
    public DateTime DecidedAt { get; private set; }
    public string Source { get; private set; } = string.Empty;

    public static Decision Record(Guid tenantId, Application application, string kind, string reason, Guid decidedBy, string source, DateTime utcNow)
        => new(NewId(), tenantId) { ApplicationId = application.Id, ProgrammeId = application.ProgrammeId, LearnerId = application.LearnerId, Kind = kind, Reason = reason.Trim(), DecidedBy = decidedBy, Source = source, DecidedAt = utcNow };
}

public enum EntryAttemptStatus
{
    InProgress = 0,
    Submitted = 1,
    Expired = 2,
}

/// <summary>Entry assessment attempt (story F3): server-side timing identical to Lms attempts (deadline + 30 s grace).</summary>
public sealed class EntryAttempt : Entity
{
    private EntryAttempt()
    {
    }

    private EntryAttempt(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid ApplicationId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime DeadlineAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public EntryAttemptStatus Status { get; private set; }
    public string AnswersJson { get; private set; } = "{}";
    public decimal Score { get; private set; }
    public decimal MaxScore { get; private set; }
    public bool Passed { get; private set; }

    public static EntryAttempt Start(Guid tenantId, Guid applicationId, int timeLimitMinutes, DateTime utcNow)
        => new(NewId(), tenantId) { ApplicationId = applicationId, StartedAt = utcNow, DeadlineAt = utcNow.AddMinutes(timeLimitMinutes), Status = EntryAttemptStatus.InProgress };

    public Result Submit(string answersJson, decimal score, decimal maxScore, bool passed, DateTime utcNow)
    {
        if (Status != EntryAttemptStatus.InProgress)
        {
            return Error.Conflict("assessment_closed", "This assessment is already submitted or expired.");
        }

        Status = utcNow > DeadlineAt.AddSeconds(30) ? EntryAttemptStatus.Expired : EntryAttemptStatus.Submitted;
        AnswersJson = answersJson;
        SubmittedAt = utcNow;
        Score = score;
        MaxScore = maxScore;
        Passed = Status == EntryAttemptStatus.Submitted && passed;
        return Result.Success();
    }
}

public enum AppealStatus
{
    Open = 0,
    Upheld = 1,
    Dismissed = 2,
}

public sealed class Appeal : Entity
{
    private Appeal()
    {
    }

    private Appeal(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid ApplicationId { get; private set; }
    public Guid ProgrammeId { get; private set; }
    public Guid LearnerId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public AppealStatus Status { get; private set; }
    public DateTime FiledAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public string? Resolution { get; private set; }

    public static Appeal File(Guid tenantId, Application application, string reason, DateTime utcNow)
        => new(NewId(), tenantId) { ApplicationId = application.Id, ProgrammeId = application.ProgrammeId, LearnerId = application.LearnerId, Reason = reason.Trim(), Status = AppealStatus.Open, FiledAt = utcNow };

    public Result Resolve(bool upheld, string resolution, Guid staffId, DateTime utcNow)
    {
        if (Status != AppealStatus.Open)
        {
            return Error.Conflict("appeal_already_resolved", "This appeal is already resolved.");
        }

        Status = upheld ? AppealStatus.Upheld : AppealStatus.Dismissed;
        Resolution = resolution.Trim();
        ResolvedBy = staffId;
        ResolvedAt = utcNow;
        return Result.Success();
    }
}

/// <summary>Shortlist sign-off by the sponsor (or by Plastoids when selection is delegated) before offers go out.</summary>
public sealed class ShortlistApproval : Entity
{
    private ShortlistApproval()
    {
    }

    private ShortlistApproval(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid ProgrammeId { get; private set; }
    public Guid ApprovedBy { get; private set; }
    public DateTime ApprovedAt { get; private set; }
    public bool Delegated { get; private set; }
    public int AcceptedCount { get; private set; }

    public static ShortlistApproval Record(Guid tenantId, Guid programmeId, Guid approvedBy, bool delegated, int acceptedCount, DateTime utcNow)
        => new(NewId(), tenantId) { ProgrammeId = programmeId, ApprovedBy = approvedBy, Delegated = delegated, AcceptedCount = acceptedCount, ApprovedAt = utcNow };
}

/// <summary>Local note of funded programmes (sponsorship.programme-funded.v1) for the review queue summary.</summary>
public sealed class ProgrammeRef : Entity
{
    private ProgrammeRef()
    {
    }

    private ProgrammeRef(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid ProgrammeId { get; private set; }
    public int SeatsFunded { get; private set; }

    public static ProgrammeRef Record(Guid tenantId, Guid programmeId, int seatsFunded) => new(NewId(), tenantId) { ProgrammeId = programmeId, SeatsFunded = seatsFunded };
    public void Update(int seatsFunded) => SeatsFunded = seatsFunded;
}
