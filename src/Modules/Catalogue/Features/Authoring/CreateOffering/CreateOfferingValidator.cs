using FluentValidation;
using Plastoids.Modules.Catalogue.Domain;

namespace Plastoids.Modules.Catalogue.Features.Authoring.CreateOffering;

internal sealed class CreateOfferingValidator : AbstractValidator<CreateOfferingCommand>
{
    public CreateOfferingValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Format).Must(OfferingFormats.IsValid).WithMessage("format must be self_paced, cohort or one_to_one.");
        RuleFor(x => x.Price.AmountKobo).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1_000_000_000);
        RuleFor(x => x.FundingTypes).NotEmpty();
        RuleFor(x => x.Capacity).InclusiveBetween(1, 10_000).When(x => x.Capacity is not null);
        RuleFor(x => x.DurationWeeks).InclusiveBetween(1, 104).When(x => x.DurationWeeks is not null);
    }
}
