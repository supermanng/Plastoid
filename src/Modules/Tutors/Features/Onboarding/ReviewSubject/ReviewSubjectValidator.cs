using FluentValidation;
using Plastoids.Modules.Tutors.Features.Onboarding.ReviewCredential;

namespace Plastoids.Modules.Tutors.Features.Onboarding.ReviewSubject;

internal sealed class ReviewSubjectValidator : AbstractValidator<ReviewSubjectCommand>
{
    public ReviewSubjectValidator()
    {
        RuleFor(x => x.TutorId).NotEmpty();
        RuleFor(x => x.Decision).Must(d => d is ReviewDecisions.Approved or ReviewDecisions.Rejected).WithMessage("decision must be approved or rejected.");
        RuleFor(x => x.Score).InclusiveBetween(0, 100).When(x => x.Score is not null);
        RuleFor(x => x.Note).NotEmpty().MaximumLength(500).When(x => x.Decision == ReviewDecisions.Rejected);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
