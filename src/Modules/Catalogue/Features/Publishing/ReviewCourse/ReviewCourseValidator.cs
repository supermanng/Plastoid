using FluentValidation;

namespace Plastoids.Modules.Catalogue.Features.Publishing.ReviewCourse;

internal sealed class ReviewCourseValidator : AbstractValidator<ReviewCourseCommand>
{
    public ReviewCourseValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Decision).Must(d => d is "approved" or "rejected").WithMessage("decision must be approved or rejected.");
        RuleFor(x => x.Note).NotEmpty().MaximumLength(500).When(x => x.Decision == "rejected").WithMessage("A note is required when rejecting.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
