using Plastoids.SharedKernel.Results;

namespace Plastoids.Infrastructure.Integrations;

public sealed record CalendarConnection(string Provider, string ExternalCalendarId, string AccountEmail);

/// <summary>Google Calendar / Microsoft Graph two-way availability sync (blueprint §17). Fake locally; Google stub.</summary>
public interface ICalendarProvider
{
    Task<Result<CalendarConnection>> ConnectAsync(string authorizationCode, CancellationToken cancellationToken);
}

public sealed class FakeCalendarProvider : ICalendarProvider
{
    public Task<Result<CalendarConnection>> ConnectAsync(string authorizationCode, CancellationToken cancellationToken)
        => Task.FromResult(string.IsNullOrWhiteSpace(authorizationCode) || authorizationCode == "invalid"
            ? Result.Failure<CalendarConnection>(Error.Validation("calendar_code_invalid", "The authorization code was rejected."))
            : Result.Success(new CalendarConnection("google-fake", "primary", "tutor@example.com")));
}

internal sealed class GoogleCalendarProvider(Secrets.ISecretProvider secrets) : ICalendarProvider
{
    public Task<Result<CalendarConnection>> ConnectAsync(string authorizationCode, CancellationToken cancellationToken)
    {
        _ = secrets.GetRequired("GoogleOAuthClientSecret");
        return Task.FromResult(Result.Failure<CalendarConnection>(Error.Failure("calendar_provider_not_implemented", "Google Calendar OAuth exchange is a stub.")));
    }
}
