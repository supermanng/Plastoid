using FluentValidation;
using Plastoids.Modules.Tutors.Domain;

namespace Plastoids.Modules.Tutors.Features.Onboarding.UploadCredential;

internal sealed class UploadCredentialValidator : AbstractValidator<UploadCredentialCommand>
{
    private static readonly string[] AllowedContentTypes = ["application/pdf", "image/jpeg", "image/png"];

    public UploadCredentialValidator()
    {
        RuleFor(x => x.Type).NotEmpty().Must(CredentialTypes.IsValid).WithMessage("type must be degree, certificate, work_history or linkedin.");
        RuleFor(x => x.Issuer).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContentType).NotEmpty().Must(c => AllowedContentTypes.Contains(c)).WithMessage("Upload a PDF, JPEG or PNG.");
    }
}
