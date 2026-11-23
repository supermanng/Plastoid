using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Sponsorship.Services;

/// <summary>
/// How one seat's price splits when it is allocated (ADR 0021 §3, "exactly like a sale"): commission at the course rate
/// (<c>course_platform</c>, or <c>course_provider</c> for an organisation course), the engaged tutor's share of the net on an
/// organisation course, and the rest to the payee (the organisation, or the tutor who owns the course).
/// </summary>
internal sealed class SeatPricing(ICourseOutlineReader courses, IEngagementReader engagements, IFeeRules fees)
{
    public async Task<Result<SeatSplit>> SplitAsync(SeatBlock block, CancellationToken cancellationToken)
    {
        var course = await courses.GetPublishedAsync(block.CourseId, null, cancellationToken);
        if (course is null)
        {
            return Error.Conflict("course_not_published", "The block's course is no longer published.");
        }

        var offering = course.Offerings.SingleOrDefault(o => o.OfferingId == block.OfferingId);
        var providerSale = course.OwnerType == CourseOwnerTypes.Organisation;
        var decision = fees.Resolve(new SaleContext(PaymentPurposes.Course, LearnerSources.Platform, ProviderSale: providerSale));
        EngagementTerms? terms = null;
        if (providerSale && offering?.EngagementId is { } engagementId)
        {
            var engagement = await engagements.GetActiveAsync(engagementId, cancellationToken);
            if (engagement is not null && engagement.TutorUserId == (offering.TutorUserId ?? block.TutorUserId))
            {
                terms = engagement.Terms;
            }
        }

        return Compute(block.PricePerSeat, decision.CommissionBps, course.OwnerType, course.OwnerUserId, course.OrganisationId, terms);
    }

    /// <summary>Pure split: commission = price × bps (half to even); organisation course → tutor share of the net per the terms, organisation nets the rest; tutor course → the owner nets price − commission.</summary>
    public static SeatSplit Compute(Money price, int commissionBps, string ownerType, Guid ownerUserId, Guid? organisationId, EngagementTerms? terms)
    {
        var commission = new Money((long)Math.Round((decimal)price.AmountKobo * commissionBps / 10_000m, 0, MidpointRounding.ToEven), price.Currency);
        var net = price - commission;
        if (ownerType == CourseOwnerTypes.Organisation && organisationId is { } organisation)
        {
            var tutorShare = terms?.TutorShareOf(net) ?? Money.Zero(price.Currency);
            return new SeatSplit(commission, tutorShare, net - tutorShare, PayeeTypes.Organisation, organisation);
        }

        return new SeatSplit(commission, Money.Zero(price.Currency), net, PayeeTypes.Tutor, ownerUserId);
    }
}
