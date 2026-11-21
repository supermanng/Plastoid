using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Contracts;

/// <summary>What Payments needs to charge a quoted price (blueprint §9.2: price honoured 30 min after being seen).</summary>
public sealed record QuotedOffering(string QuoteId, Guid OfferingId, Guid CourseId, Guid CourseOwnerUserId, string CourseTitle, Money Price, DateTime ValidUntil);

public interface IPriceQuoteReader
{
    Task<QuotedOffering?> GetAsync(string quoteId, CancellationToken cancellationToken);
}
