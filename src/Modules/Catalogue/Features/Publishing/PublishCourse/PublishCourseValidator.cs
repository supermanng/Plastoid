using FluentValidation;

namespace Plastoids.Modules.Catalogue.Features.Publishing.PublishCourse;

internal sealed class PublishCourseValidator : AbstractValidator<PublishCourseCommand>
{
    public PublishCourseValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
    }
}
