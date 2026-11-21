using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Tutors.Features.Profile.ConnectGoogleCalendar;

public sealed record ConnectGoogleCalendarCommand(string AuthorizationCode) : ICommand<ConnectGoogleCalendarResponse>;

public sealed record ConnectGoogleCalendarResponse(string Provider, string Account);
