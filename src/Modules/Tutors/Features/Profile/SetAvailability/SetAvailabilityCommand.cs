using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Tutors.Features.Profile.SetAvailability;

public sealed record WeeklyWindow(DayOfWeek Day, TimeOnly Start, TimeOnly End);

public sealed record DatedException(DateOnly Date, TimeOnly? Start, TimeOnly? End, bool Available);

/// <summary>Replaces the tutor's weekly rules and exceptions (local times in the given IANA time zone).</summary>
public sealed record SetAvailabilityCommand(string TimeZone, IReadOnlyList<WeeklyWindow> Weekly, IReadOnlyList<DatedException> Exceptions, int BufferMinutes = 0) : ICommand<SetAvailabilityResponse>;

public sealed record SetAvailabilityResponse(int WeeklyRules, int Exceptions, string TimeZone);
