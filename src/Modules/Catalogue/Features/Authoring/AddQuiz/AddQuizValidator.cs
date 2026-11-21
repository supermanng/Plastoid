using FluentValidation;

namespace Plastoids.Modules.Catalogue.Features.Authoring.AddQuiz;

internal sealed class AddQuizValidator : AbstractValidator<AddQuizCommand>
{
    public AddQuizValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PassMarkPercent).InclusiveBetween(1, 100);
        RuleFor(x => x.Questions).NotEmpty().Must(q => q.Count <= 100);
        RuleForEach(x => x.Questions).ChildRules(q =>
        {
            q.RuleFor(x => x.Prompt).NotEmpty().MaximumLength(1000);
            q.RuleFor(x => x.Options).Must(o => o.Count is >= 2 and <= 6).WithMessage("Each question needs 2 to 6 options.");
            q.RuleFor(x => x).Must(x => x.CorrectIndex >= 0 && x.CorrectIndex < x.Options.Count).WithMessage("correctIndex must point at one of the options.");
        });
    }
}
