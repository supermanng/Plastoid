using FluentValidation;
using Plastoids.Modules.Identity.Domain;

namespace Plastoids.Modules.Identity.Features.Auth.VerifyOtp;

internal sealed class VerifyOtpValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().Must(p => PhoneNumber.TryNormalise(p, out _)).WithMessage("Enter a valid phone number.");
        RuleFor(x => x.Code).NotEmpty().Length(6).Matches("^[0-9]{6}$");
        RuleFor(x => x.DisplayName).MaximumLength(120);
    }
}
