using FluentValidation;
using Plastoids.Modules.Tutors.Domain;

namespace Plastoids.Modules.Tutors.Features.Profile.SetPricing;

internal sealed class SetPricingValidator : AbstractValidator<SetPricingCommand>
{
    public SetPricingValidator()
    {
        RuleFor(x => x.HourlyRate.AmountKobo).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100_000_000);
        RuleFor(x => x.HourlyRate.Currency).Equal("NGN");
        RuleFor(x => x.Rules).NotNull().Must(r => r.Count <= 20);
        RuleForEach(x => x.Rules).ChildRules(r =>
        {
            r.RuleFor(p => p.Format).Must(TeachingFormats.IsValid).WithMessage("format must be one_to_one, group or course.");
            r.RuleFor(p => p.DurationMinutes).InclusiveBetween(15, 480);
            r.RuleFor(p => p.Price.AmountKobo).GreaterThanOrEqualTo(0);
            r.RuleFor(p => p.MaxLearners).InclusiveBetween(1, 500).When(p => p.MaxLearners is not null);
        });
    }
}
