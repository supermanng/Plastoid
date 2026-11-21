using FluentValidation;

namespace Plastoids.Modules.Tutors.Features.Profile.ConnectGoogleCalendar;

internal sealed class ConnectGoogleCalendarValidator : AbstractValidator<ConnectGoogleCalendarCommand>
{
    public ConnectGoogleCalendarValidator()
    {
        RuleFor(x => x.AuthorizationCode).NotEmpty().MaximumLength(2048);
    }
}
