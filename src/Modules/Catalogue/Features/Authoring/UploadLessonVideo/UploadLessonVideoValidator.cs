using FluentValidation;

namespace Plastoids.Modules.Catalogue.Features.Authoring.UploadLessonVideo;

internal sealed class UploadLessonVideoValidator : AbstractValidator<UploadLessonVideoCommand>
{
    public UploadLessonVideoValidator()
    {
        RuleFor(x => x.LessonId).NotEmpty();
    }
}

internal sealed class VideoWebhookValidator : AbstractValidator<VideoWebhookCommand>
{
    public VideoWebhookValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(32);
        RuleFor(x => x.UploadId).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Status).Must(s => s is "ready" or "failed" or "processing").WithMessage("status must be processing, ready or failed.");
        RuleFor(x => x.PlaybackId).NotEmpty().When(x => x.Status == "ready");
    }
}
