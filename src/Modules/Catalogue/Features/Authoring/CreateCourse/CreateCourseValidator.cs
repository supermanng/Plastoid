using FluentValidation;
using Plastoids.Modules.Catalogue.Domain;

namespace Plastoids.Modules.Catalogue.Features.Authoring.CreateCourse;

internal sealed class CreateCourseValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MinimumLength(5).MaximumLength(200);
        RuleFor(x => x.Summary).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Level).Must(CourseLevels.IsValid).WithMessage("level must be beginner, intermediate or advanced.");
        RuleFor(x => x.Language).NotEmpty().Length(2, 8);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Skills).NotNull().Must(s => s.Count <= 15);
        RuleForEach(x => x.Skills).NotEmpty().MaximumLength(60);
    }
}
