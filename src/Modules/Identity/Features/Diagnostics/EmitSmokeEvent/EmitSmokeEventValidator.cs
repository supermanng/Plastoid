using FluentValidation;

namespace Plastoids.Modules.Identity.Features.Diagnostics.EmitSmokeEvent;

internal sealed class EmitSmokeEventValidator : AbstractValidator<EmitSmokeEventCommand>
{
    public EmitSmokeEventValidator()
    {
        RuleFor(x => x.Note).NotEmpty().MaximumLength(200);
    }
}
