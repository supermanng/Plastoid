using FluentValidation;

namespace Plastoids.Modules.Tutors.Features.Onboarding.SubmitIdentityCheck;

internal sealed class SubmitIdentityCheckValidator : AbstractValidator<SubmitIdentityCheckCommand>
{
    public SubmitIdentityCheckValidator()
    {
        RuleFor(x => x.Nin).NotEmpty().Matches("^[0-9]{11}$").WithMessage("NIN is 11 digits.");
        RuleFor(x => x.Bvn).NotEmpty().Matches("^[0-9]{11}$").WithMessage("BVN is 11 digits.");
        RuleFor(x => x.SelfieBlobKey).NotEmpty().MaximumLength(300);
        RuleFor(x => x.FullName).NotEmpty().MinimumLength(3).MaximumLength(160);
    }
}
