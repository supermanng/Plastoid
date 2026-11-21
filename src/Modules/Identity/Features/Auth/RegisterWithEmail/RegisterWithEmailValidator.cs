using FluentValidation;

namespace Plastoids.Modules.Identity.Features.Auth.RegisterWithEmail;

internal sealed class RegisterWithEmailValidator : AbstractValidator<RegisterWithEmailCommand>
{
    public RegisterWithEmailValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10).MaximumLength(128)
            .Must(p => p.Any(char.IsLetter) && p.Any(char.IsDigit)).WithMessage("Password needs at least one letter and one digit.");
    }
}
