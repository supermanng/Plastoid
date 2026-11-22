using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Contracts;

/// <summary>What Payments needs to charge a quoted price (blueprint §9.2: price honoured 30 min after being seen).</summary>
/// <summary>
/// CourseOwnerUserId is the tutor paid for a tutor-owned course. For an organisation course (ADR 0019) OrganisationId is the payee,
/// TutorUserId the engaged tutor who teaches the offering (null for an unassigned self-paced offering) and TutorTerms their engagement terms.
/// </summary>
public sealed record QuotedOffering(string QuoteId, Guid OfferingId, Guid CourseId, Guid CourseOwnerUserId, string CourseTitle, Money Price, DateTime ValidUntil,
    string CourseOwnerType = CourseOwnerTypes.Tutor, Guid? OrganisationId = null, Guid? TutorUserId = null, Guid? EngagementId = null, EngagementTerms? TutorTerms = null);

public interface IPriceQuoteReader
{
    Task<QuotedOffering?> GetAsync(string quoteId, CancellationToken cancellationToken);
}
