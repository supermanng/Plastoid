using FluentValidation;
using Plastoids.Modules.Tutors.Domain;

namespace Plastoids.Modules.Tutors.Features.Onboarding.ApplyAsTutor;

internal sealed class ApplyAsTutorValidator : AbstractValidator<ApplyAsTutorCommand>
{
    public ApplyAsTutorValidator()
    {
        RuleFor(x => x.Headline).NotEmpty().MinimumLength(10).MaximumLength(160);
        RuleFor(x => x.Bio).NotEmpty().MinimumLength(50).MaximumLength(4000);
        RuleFor(x => x.Subjects).NotEmpty().Must(s => s.Count <= 20);
        RuleForEach(x => x.Subjects).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Languages).NotEmpty();
        RuleFor(x => x.Formats).NotEmpty().Must(f => f.All(TeachingFormats.IsValid)).WithMessage("formats must be one_to_one, group or course.");
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(64);
        RuleFor(x => x.HourlyRate.AmountKobo).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100_000_000);
        RuleFor(x => x.AcceptCodeOfConduct).Equal(true).WithMessage("You must accept the tutor code of conduct.");
    }
}
