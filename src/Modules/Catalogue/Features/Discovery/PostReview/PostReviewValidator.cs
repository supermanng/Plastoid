using FluentValidation;

namespace Plastoids.Modules.Catalogue.Features.Discovery.PostReview;

internal sealed class PostReviewValidator : AbstractValidator<PostReviewCommand>
{
    public PostReviewValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.EnrolmentId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Body).NotEmpty().MinimumLength(10).MaximumLength(2000);
    }
}
