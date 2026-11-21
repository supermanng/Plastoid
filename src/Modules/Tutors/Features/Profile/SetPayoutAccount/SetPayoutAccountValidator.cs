using FluentValidation;

namespace Plastoids.Modules.Tutors.Features.Profile.SetPayoutAccount;

internal sealed class SetPayoutAccountValidator : AbstractValidator<SetPayoutAccountCommand>
{
    public SetPayoutAccountValidator()
    {
        RuleFor(x => x.BankCode).NotEmpty().Matches("^[0-9]{3,6}$").WithMessage("Use the CBN bank code (3–6 digits).");
        RuleFor(x => x.AccountNumber).NotEmpty().Matches("^[0-9]{10}$").WithMessage("NUBAN account numbers have 10 digits.");
    }
}
