using FluentValidation;

namespace Plastoids.Modules.Tutors.Features.Onboarding.ReviewCredential;

internal sealed class ReviewCredentialValidator : AbstractValidator<ReviewCredentialCommand>
{
    public ReviewCredentialValidator()
    {
        RuleFor(x => x.CredentialId).NotEmpty();
        RuleFor(x => x.Decision).Must(d => d is ReviewDecisions.Approved or ReviewDecisions.Rejected).WithMessage("decision must be approved or rejected.");
        RuleFor(x => x.Note).NotEmpty().MaximumLength(500).When(x => x.Decision == ReviewDecisions.Rejected).WithMessage("A note is required when rejecting.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
