using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Identity.Domain;

/// <summary>Opaque, rotating refresh token. Only the SHA-256 of the token is stored.</summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    private RefreshToken(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByHash { get; private set; }
    public string? CreatedByIp { get; private set; }

    /// <summary>Authentication methods of the session that minted it, carried over on rotation (e.g. otp, pwd, mfa).</summary>
    public string Amr { get; private set; } = string.Empty;

    public static RefreshToken Issue(Guid tenantId, Guid userId, string tokenHash, DateTime expiresAt, string? ip, IEnumerable<string> amr)
        => new(NewId(), tenantId) { UserId = userId, TokenHash = tokenHash, ExpiresAt = expiresAt, CreatedByIp = ip, Amr = string.Join(' ', amr) };

    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;

    public void Revoke(DateTime utcNow, string? replacedByHash = null)
    {
        RevokedAt = utcNow;
        ReplacedByHash = replacedByHash;
    }

    public IReadOnlyCollection<string> AmrValues => Amr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
