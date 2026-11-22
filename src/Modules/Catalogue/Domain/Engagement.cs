using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Catalogue.Domain;

public enum EngagementStatus
{
    Offered = 0,
    Active = 1,
    Declined = 2,
    Ended = 3,
}

/// <summary>
/// ADR 0019: an organisation offers a verified tutor terms to teach one of its courses. offered → active | declined; active → ended.
/// One offered-or-active engagement per course and tutor (enforced by the OfferEngagement handler: MySQL has no filtered unique index).
/// </summary>
[Audited]
public sealed class Engagement : Entity
{
    public const int MaxBps = 10_000;

    private Engagement()
    {
    }

    private Engagement(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid CourseId { get; private set; }
    public Guid OrganisationId { get; private set; }
    public Guid TutorId { get; private set; }
    public Guid TutorUserId { get; private set; }

    /// <summary>An <see cref="EngagementTermKinds"/> value.</summary>
    public string TermsKind { get; private set; } = EngagementTermKinds.RevenueShare;

    /// <summary>Basis points of the sale net of commission (revenue_share); 0 for per_enrolment.</summary>
    public int ShareBps { get; private set; }

    /// <summary>Fixed naira fee per paid enrolment in kobo (per_enrolment); null for revenue_share.</summary>
    public long? FixedFeeKobo { get; private set; }

    public string? FixedFeeCurrency { get; private set; }
    public EngagementStatus Status { get; private set; }
    public string? Note { get; private set; }
    public Guid OfferedBy { get; private set; }
    public DateTime OfferedAt { get; private set; }
    public DateTime? RespondedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public string? EndReason { get; private set; }

    public bool IsOpen => Status is EngagementStatus.Offered or EngagementStatus.Active;

    public Money? FixedFee => FixedFeeKobo is { } kobo ? new Money(kobo, FixedFeeCurrency ?? Money.Naira) : null;

    public EngagementTerms ToTerms() => new(TermsKind, ShareBps, FixedFee);

    public static Result<Engagement> Offer(Guid tenantId, Guid courseId, Guid organisationId, Guid tutorId, Guid tutorUserId, EngagementTerms terms, string? note, Guid offeredBy, DateTime utcNow)
    {
        if (!EngagementTermKinds.IsValid(terms.Kind))
        {
            return Error.Validation("engagement_terms_invalid", "terms.kind must be revenue_share or per_enrolment.");
        }

        if (terms.Kind == EngagementTermKinds.RevenueShare && terms.ShareBps is < 1 or > MaxBps)
        {
            return Error.Validation("engagement_share_invalid", "revenue_share needs shareBps between 1 and 10000.");
        }

        if (terms.Kind == EngagementTermKinds.PerEnrolment && (terms.FixedFee is not { } fee || fee.IsZero || fee.IsNegative || fee.Currency != Money.Naira))
        {
            return Error.Validation("engagement_fee_invalid", "per_enrolment needs a positive naira fixedFee.");
        }

        var perEnrolment = terms.Kind == EngagementTermKinds.PerEnrolment;
        return new Engagement(NewId(), tenantId)
        {
            CourseId = courseId,
            OrganisationId = organisationId,
            TutorId = tutorId,
            TutorUserId = tutorUserId,
            TermsKind = terms.Kind,
            ShareBps = perEnrolment ? 0 : terms.ShareBps,
            FixedFeeKobo = perEnrolment ? terms.FixedFee!.Value.AmountKobo : null,
            FixedFeeCurrency = perEnrolment ? terms.FixedFee!.Value.Currency : null,
            Status = EngagementStatus.Offered,
            Note = Trim(note),
            OfferedBy = offeredBy,
            OfferedAt = utcNow,
        };
    }

    public Result Accept(string? note, DateTime utcNow)
    {
        if (Status != EngagementStatus.Offered)
        {
            return Error.Conflict("engagement_not_offered", "Only an offered engagement can be accepted.");
        }

        Status = EngagementStatus.Active;
        RespondedAt = utcNow;
        Note = Trim(note) ?? Note;
        return Result.Success();
    }

    public Result Decline(string? note, DateTime utcNow)
    {
        if (Status != EngagementStatus.Offered)
        {
            return Error.Conflict("engagement_not_offered", "Only an offered engagement can be declined.");
        }

        Status = EngagementStatus.Declined;
        RespondedAt = utcNow;
        Note = Trim(note) ?? Note;
        return Result.Success();
    }

    public Result End(string? reason, DateTime utcNow)
    {
        if (Status != EngagementStatus.Active)
        {
            return Error.Conflict("engagement_not_active", "Only an active engagement can be ended.");
        }

        Status = EngagementStatus.Ended;
        EndedAt = utcNow;
        EndReason = Trim(reason);
        return Result.Success();
    }

    private static string? Trim(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
