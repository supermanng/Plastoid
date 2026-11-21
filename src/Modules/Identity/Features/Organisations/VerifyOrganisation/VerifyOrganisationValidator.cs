using FluentValidation;

namespace Plastoids.Modules.Identity.Features.Organisations.VerifyOrganisation;

internal sealed class VerifyOrganisationValidator : AbstractValidator<VerifyOrganisationCommand>
{
    public VerifyOrganisationValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
        RuleFor(x => x.Decision).NotEmpty().Must(d => d is VerificationDecisions.Verified or VerificationDecisions.Rejected).WithMessage("decision must be verified or rejected.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).When(x => x.Decision == VerificationDecisions.Rejected).WithMessage("A reason is required when rejecting.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
