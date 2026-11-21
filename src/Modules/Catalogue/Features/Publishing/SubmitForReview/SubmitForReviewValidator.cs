using FluentValidation;

namespace Plastoids.Modules.Catalogue.Features.Publishing.SubmitForReview;

internal sealed class SubmitForReviewValidator : AbstractValidator<SubmitForReviewCommand>
{
    public SubmitForReviewValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
    }
}
