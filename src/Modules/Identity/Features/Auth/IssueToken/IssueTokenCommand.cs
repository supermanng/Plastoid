using Plastoids.Modules.Identity.Services;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Auth.IssueToken;

public static class GrantTypes
{
    public const string Password = "password";
    public const string RefreshToken = "refresh_token";
    public const string Google = "google";
}

/// <summary>OAuth-style token endpoint: password, refresh_token or google grants (ADR 0007).</summary>
public sealed record IssueTokenCommand(string GrantType, string? Email, string? Password, string? RefreshToken, string? IdToken) : ICommand<TokenResponse>;
