using FluentValidation;
using Plastoids.Modules.Catalogue.Domain;

namespace Plastoids.Modules.Catalogue.Features.Authoring.UpdateCourseOutline;

internal sealed class UpdateCourseOutlineValidator : AbstractValidator<UpdateCourseOutlineCommand>
{
    public UpdateCourseOutlineValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Title).MinimumLength(5).MaximumLength(200).When(x => x.Title is not null);
        RuleFor(x => x.Summary).MaximumLength(4000).When(x => x.Summary is not null);
        RuleFor(x => x.Level).Must(l => CourseLevels.IsValid(l!)).When(x => x.Level is not null).WithMessage("level must be beginner, intermediate or advanced.");
        RuleFor(x => x.Modules).NotNull().Must(m => m.Count <= 50);
        RuleForEach(x => x.Modules).ChildRules(m => m.RuleFor(x => x.Title).NotEmpty().MaximumLength(200));
    }
}
