using FluentValidation;

namespace Plastoids.Modules.Catalogue.Features.Engagements.EndEngagement;

internal sealed class EndEngagementValidator : AbstractValidator<EndEngagementCommand>
{
    public EndEngagementValidator()
    {
        RuleFor(x => x.EngagementId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
