using FluentValidation;
using Plastoids.Modules.Identity.Domain;

namespace Plastoids.Modules.Identity.Features.Organisations.ChangeMemberRole;

internal sealed class ChangeMemberRoleValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    public ChangeMemberRoleValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Role).NotEmpty().Must(OrganisationRoles.IsValid).WithMessage("role must be owner, finance, reviewer, viewer or author.");
    }
}
