using FluentValidation;

namespace Plastoids.Modules.Identity.Features.Me.UpdateGoals;

internal sealed class UpdateGoalsValidator : AbstractValidator<UpdateGoalsCommand>
{
    public UpdateGoalsValidator()
    {
        RuleFor(x => x.CareerGoal).MaximumLength(200);
        RuleFor(x => x.Interests).NotNull().Must(i => i.Count <= 20).WithMessage("At most 20 interests.");
        RuleForEach(x => x.Interests).NotEmpty().MaximumLength(60);
    }
}
