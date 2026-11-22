using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;

namespace Plastoids.Modules.Catalogue.Services;

/// <summary>Read side of the price quote for Payments. Joins the caller's transaction (shared connection) when one is open.</summary>
internal sealed class PriceQuoteReader(PriceQuotes quotes, CatalogueDbContext db, IUnitOfWork unitOfWork) : IPriceQuoteReader
{
    public async Task<QuotedOffering?> GetAsync(string quoteId, CancellationToken cancellationToken)
    {
        var quote = await quotes.GetAsync(quoteId);
        if (quote is null)
        {
            return null;
        }

        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        var row = await db.Offerings.AsNoTracking().Where(o => o.Id == quote.OfferingId)
            .Join(db.Courses.AsNoTracking(), o => o.CourseId, c => c.Id, (o, c) => new { o.Id, c.OwnerUserId, CourseId = c.Id, c.Title, c.OwnerType, c.OrganisationId, o.TutorUserId, o.EngagementId })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        // The tutor share applies only while the offering's engagement is still active; otherwise the organisation keeps the whole net (ADR 0019 §5).
        var engagement = row.EngagementId is { } engagementId
            ? await db.Engagements.AsNoTracking().SingleOrDefaultAsync(e => e.Id == engagementId && e.Status == EngagementStatus.Active, cancellationToken)
            : null;
        return new QuotedOffering(quote.QuoteId, row.Id, row.CourseId, row.OwnerUserId, row.Title, quote.Price, quote.ValidUntil,
            row.OwnerType.ToString().ToLowerInvariant(), row.OrganisationId, engagement?.TutorUserId, engagement?.Id, engagement?.ToTerms());
    }
}
