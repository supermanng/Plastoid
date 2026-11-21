using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Identity.Services;

/// <summary>Verified claims from a Google ID token.</summary>
public sealed record ExternalIdentity(string Subject, string Email, bool EmailVerified, string? Name);

/// <summary>Google sign-in adapter. Stub until a client id is configured (Auth:Google:ClientId); the IssueToken slice is already wired.</summary>
public interface IGoogleIdTokenVerifier
{
    Task<Result<ExternalIdentity>> VerifyAsync(string idToken, CancellationToken cancellationToken);
}

internal sealed class NotConfiguredGoogleIdTokenVerifier : IGoogleIdTokenVerifier
{
    public Task<Result<ExternalIdentity>> VerifyAsync(string idToken, CancellationToken cancellationToken)
        => Task.FromResult(Result.Failure<ExternalIdentity>(Error.Failure("google_sign_in_not_configured", "Google sign-in is not configured on this environment.")));
}
