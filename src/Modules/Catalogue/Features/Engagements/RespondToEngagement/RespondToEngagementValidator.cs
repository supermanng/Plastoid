using FluentValidation;

namespace Plastoids.Modules.Catalogue.Features.Engagements.RespondToEngagement;

internal sealed class RespondToEngagementValidator : AbstractValidator<RespondToEngagementCommand>
{
    public RespondToEngagementValidator()
    {
        RuleFor(x => x.EngagementId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
