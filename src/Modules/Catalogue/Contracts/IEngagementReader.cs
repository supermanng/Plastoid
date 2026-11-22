using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Contracts;

/// <summary>How an engaged tutor is paid per sale of an organisation course (ADR 0019).</summary>
public static class EngagementTermKinds
{
    /// <summary>ShareBps of the sale net of platform commission.</summary>
    public const string RevenueShare = "revenue_share";

    /// <summary>FixedFee per paid enrolment, capped at the sale net of platform commission.</summary>
    public const string PerEnrolment = "per_enrolment";
    public static readonly IReadOnlyCollection<string> All = [RevenueShare, PerEnrolment];
    public static bool IsValid(string kind) => All.Contains(kind, StringComparer.Ordinal);
}

/// <summary>Kind is an <see cref="EngagementTermKinds"/> value; ShareBps applies to revenue_share, FixedFee to per_enrolment.</summary>
public sealed record EngagementTerms(string Kind, int ShareBps, Money? FixedFee)
{
    /// <summary>The tutor's part of <paramref name="netOfCommission"/>: never negative, never more than the net. Half-kobo rounds to even.</summary>
    public Money TutorShareOf(Money netOfCommission)
    {
        if (netOfCommission.IsNegative || netOfCommission.IsZero)
        {
            return Money.Zero(netOfCommission.Currency);
        }

        if (Kind == EngagementTermKinds.PerEnrolment)
        {
            var fee = FixedFee ?? Money.Zero(netOfCommission.Currency);
            return fee > netOfCommission ? netOfCommission : (fee.IsNegative ? Money.Zero(netOfCommission.Currency) : fee);
        }

        var share = new Money((long)Math.Round((decimal)netOfCommission.AmountKobo * ShareBps / 10_000m, 0, MidpointRounding.ToEven), netOfCommission.Currency);
        return share > netOfCommission ? netOfCommission : share;
    }
}

public sealed record ActiveEngagement(Guid EngagementId, Guid CourseId, Guid OrganisationId, Guid TutorUserId, Guid TutorId, EngagementTerms Terms);

/// <summary>In-process read of active tutor engagements for Lms (who may teach a course) and Payments (how to split a sale).</summary>
public interface IEngagementReader
{
    Task<IReadOnlyList<ActiveEngagement>> ActiveForCourseAsync(Guid courseId, CancellationToken cancellationToken);
    Task<ActiveEngagement?> GetActiveAsync(Guid engagementId, CancellationToken cancellationToken);
    Task<bool> IsTeachingAsync(Guid courseId, Guid userId, CancellationToken cancellationToken);
}
