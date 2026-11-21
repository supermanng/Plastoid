using FluentValidation;

namespace Plastoids.Modules.Tutors.Features.Profile.SetAvailability;

internal sealed class SetAvailabilityValidator : AbstractValidator<SetAvailabilityCommand>
{
    public SetAvailabilityValidator()
    {
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(64).Must(tz => TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _)).WithMessage("Unknown IANA time zone.");
        RuleFor(x => x.Weekly).NotNull().Must(w => w.Count <= 50);
        RuleForEach(x => x.Weekly).Must(w => w.End > w.Start).WithMessage("Each window must end after it starts.");
        RuleFor(x => x.Exceptions).NotNull().Must(e => e.Count <= 100);
        RuleForEach(x => x.Exceptions).Must(e => e.Start is null && e.End is null || (e.Start is not null && e.End is not null && e.End > e.Start))
            .WithMessage("An exception is either a whole day (no times) or a window with start and end.");
        RuleFor(x => x.BufferMinutes).InclusiveBetween(0, 120);
    }
}
