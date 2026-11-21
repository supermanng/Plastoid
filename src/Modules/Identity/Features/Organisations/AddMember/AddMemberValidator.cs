using FluentValidation;
using Plastoids.Modules.Identity.Domain;

namespace Plastoids.Modules.Identity.Features.Organisations.AddMember;

internal sealed class AddMemberValidator : AbstractValidator<AddMemberCommand>
{
    public AddMemberValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Phone)).WithMessage("Send the member's email or phone.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).Must(p => PhoneNumber.TryNormalise(p, out _)).When(x => !string.IsNullOrWhiteSpace(x.Phone)).WithMessage("Enter a valid phone number.");
        RuleFor(x => x.Role).NotEmpty().Must(OrganisationRoles.IsValid).WithMessage("role must be owner, finance, reviewer or viewer.");
    }
}
