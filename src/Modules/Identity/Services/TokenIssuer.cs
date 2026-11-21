using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Identity.Services;

/// <summary>What every successful sign-in returns. When MFA is pending only <see cref="MfaToken"/> is set.</summary>
public sealed record TokenResponse(
    bool MfaRequired,
    string? MfaToken,
    string? AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAt,
    string TokenType = "Bearer")
{
    public static TokenResponse MfaChallenge(string mfaToken) => new(true, mfaToken, null, null, null);
}

/// <summary>Mints access + rotating refresh tokens for a user from their memberships. Writes refresh rows in the ambient unit of work.</summary>
internal sealed class TokenIssuer(IdentityDbContext db, ITokenService tokens, IOptions<AuthOptions> options, IClock clock, ICurrentUser currentUser)
{
    public async Task<TokenResponse> IssueAsync(User user, IReadOnlyCollection<string> amr, CancellationToken cancellationToken)
    {
        var memberships = await db.Memberships.Where(m => m.UserId == user.Id).ToListAsync(cancellationToken);
        var roles = memberships.Where(m => m.IsPlatformRole).Select(m => m.Role).Distinct().ToArray();
        var organisations = memberships.Where(m => m.OrganisationId is not null).Select(m => m.OrganisationId!.Value).Distinct().ToArray();

        var access = tokens.IssueAccessToken(new TokenSubject(user.Id, user.TenantId, roles, organisations, amr));

        var raw = RandomNumberGenerator.GetBytes(32);
        var refresh = Base64Url(raw);
        db.RefreshTokens.Add(RefreshToken.Issue(
            user.TenantId,
            user.Id,
            HashToken(refresh),
            clock.UtcNow.AddDays(options.Value.RefreshTokenDays),
            currentUser.IpAddress,
            amr));
        user.RecordLogin(clock.UtcNow);

        return new TokenResponse(false, null, access.Token, refresh, access.ExpiresAt);
    }

    /// <summary>Rotates a refresh token: the old one is revoked and linked to its replacement (reuse of a revoked token revokes the whole chain).</summary>
    public async Task<Result<TokenResponse>> RotateAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = HashToken(refreshToken);
        var existing = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (existing is null)
        {
            return Error.Unauthorized("refresh_token_invalid", "Refresh token is not recognised.");
        }

        var now = clock.UtcNow;
        if (!existing.IsActive(now))
        {
            // Reuse of a rotated token signals theft: revoke every active token of the user.
            var active = await db.RefreshTokens.Where(t => t.UserId == existing.UserId && t.RevokedAt == null).ToListAsync(cancellationToken);
            foreach (var token in active)
            {
                token.Revoke(now);
            }

            return Error.Unauthorized("refresh_token_revoked", "Refresh token was already used or revoked; sign in again.");
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == existing.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Error.Unauthorized("account_inactive", "This account is not active.");
        }

        var response = await IssueAsync(user, existing.AmrValues, cancellationToken);
        existing.Revoke(now, HashToken(response.RefreshToken!));
        return response;
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var active = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var token in active)
        {
            token.Revoke(now);
        }
    }

    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
