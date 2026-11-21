using FluentValidation;

namespace Plastoids.Modules.Identity.Features.Auth.VerifyMfa;

internal sealed class VerifyMfaValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
    }
}
