using FluentValidation;
using Plastoids.Modules.Catalogue.Contracts;

namespace Plastoids.Modules.Catalogue.Features.Engagements.OfferEngagement;

internal sealed class OfferEngagementValidator : AbstractValidator<OfferEngagementCommand>
{
    public OfferEngagementValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.TutorId).NotEmpty();
        RuleFor(x => x.Terms).NotNull();
        RuleFor(x => x.Terms.Kind).Must(EngagementTermKinds.IsValid).WithMessage("terms.kind must be revenue_share or per_enrolment.").When(x => x.Terms is not null);
        RuleFor(x => x.Terms.ShareBps).NotNull().InclusiveBetween(1, 10_000).When(x => x.Terms is { Kind: EngagementTermKinds.RevenueShare }).WithMessage("revenue_share needs shareBps between 1 and 10000.");
        RuleFor(x => x.Terms.FixedFee).NotNull().Must(f => f!.Value.AmountKobo > 0).When(x => x.Terms is { Kind: EngagementTermKinds.PerEnrolment }).WithMessage("per_enrolment needs a positive fixedFee.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
