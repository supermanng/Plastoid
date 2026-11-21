using FluentValidation;

namespace Plastoids.Modules.Identity.Features.Me.DeleteMyAccount;

internal sealed class DeleteMyAccountValidator : AbstractValidator<DeleteMyAccountCommand>
{
    public const string ExpectedConfirmation = "DELETE";

    public DeleteMyAccountValidator()
    {
        RuleFor(x => x.Confirmation).Equal(ExpectedConfirmation).WithMessage("Send confirmation: \"DELETE\" to erase your account.");
    }
}
