using FluentValidation;
using Plastoids.Modules.Catalogue.Domain;

namespace Plastoids.Modules.Catalogue.Features.Authoring.AddLesson;

internal sealed class AddLessonValidator : AbstractValidator<AddLessonCommand>
{
    public AddLessonValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.Type).Must(t => LessonTypes.IsValid(t) && t != LessonTypes.Quiz).WithMessage("type must be video, doc, assignment or live (quizzes use the quiz endpoint).");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Order).InclusiveBetween(0, 1000);
        RuleFor(x => x.DurationMinutes).InclusiveBetween(0, 600);
        RuleFor(x => x.ContentJson).Must(c => c is null || IsJson(c)).WithMessage("contentJson must be valid JSON.");
    }

    private static bool IsJson(string s)
    {
        try
        {
            System.Text.Json.JsonDocument.Parse(s).Dispose();
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
