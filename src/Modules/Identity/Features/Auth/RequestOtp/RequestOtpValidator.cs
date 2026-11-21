using FluentValidation;
using Plastoids.Modules.Identity.Domain;

namespace Plastoids.Modules.Identity.Features.Auth.RequestOtp;

internal sealed class RequestOtpValidator : AbstractValidator<RequestOtpCommand>
{
    public RequestOtpValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().Must(p => PhoneNumber.TryNormalise(p, out _)).WithMessage("Enter a valid phone number, e.g. 0803 123 4567 or +2348031234567.");
    }
}
