using Plastoids.Infrastructure.Integrations;
using Plastoids.Modules.Tutors.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Tutors.Features.Profile.ConnectGoogleCalendar;

internal sealed class ConnectGoogleCalendarHandler(TutorAccess access, ICalendarProvider calendar) : ICommandHandler<ConnectGoogleCalendarCommand, ConnectGoogleCalendarResponse>
{
    public async Task<Result<ConnectGoogleCalendarResponse>> Handle(ConnectGoogleCalendarCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MineAsync(cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<ConnectGoogleCalendarResponse>(mine.Error);
        }

        var connection = await calendar.ConnectAsync(request.AuthorizationCode, cancellationToken);
        if (connection.IsFailure)
        {
            return Result.Failure<ConnectGoogleCalendarResponse>(connection.Error);
        }

        mine.Value.ConnectCalendar(connection.Value.AccountEmail);
        return new ConnectGoogleCalendarResponse(connection.Value.Provider, connection.Value.AccountEmail);
    }
}
