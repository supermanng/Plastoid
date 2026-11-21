using FluentValidation;
using Plastoids.Modules.Identity.Domain;

namespace Plastoids.Modules.Identity.Features.Organisations.RegisterOrganisation;

internal sealed class RegisterOrganisationValidator : AbstractValidator<RegisterOrganisationCommand>
{
    public RegisterOrganisationValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.Type).NotEmpty().Must(t => Enum.TryParse<OrganisationType>(t, true, out _)).WithMessage("type must be sponsor, institution, employer or hub.");
        RuleFor(x => x.CacNumber).NotEmpty().Matches("^(RC|BN|IT)?[- ]?[0-9]{4,10}$").WithMessage("Enter the CAC registration number, e.g. RC1234567.");
        RuleFor(x => x.SignatoryName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.SignatoryEmail).NotEmpty().EmailAddress().MaximumLength(320);
    }
}
