using FluentValidation;

namespace Plastoids.Modules.Tutors.Features.Onboarding.SubmitDemoLesson;

internal sealed class SubmitDemoLessonValidator : AbstractValidator<SubmitDemoLessonCommand>
{
    public SubmitDemoLessonValidator()
    {
        RuleFor(x => x.ContentType).NotEmpty().Must(c => c is "video/mp4" or "video/webm").WithMessage("Upload an MP4 or WebM video.");
    }
}
