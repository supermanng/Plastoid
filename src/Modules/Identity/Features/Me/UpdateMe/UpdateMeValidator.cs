using FluentValidation;

namespace Plastoids.Modules.Identity.Features.Me.UpdateMe;

internal sealed class UpdateMeValidator : AbstractValidator<UpdateMeCommand>
{
    public UpdateMeValidator()
    {
        RuleFor(x => x.DisplayName).MinimumLength(2).MaximumLength(120).When(x => x.DisplayName is not null);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(320).When(x => x.Email is not null);
        RuleFor(x => x).Must(x => x.DisplayName is not null || x.Email is not null).WithMessage("Send at least one field to update.");
    }
}
