using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Identity.Domain;

/// <summary>Single-use MFA recovery code (story A2: recovery codes issued). Only the hash is stored.</summary>
public sealed class MfaRecoveryCode : Entity
{
    private MfaRecoveryCode()
    {
    }

    private MfaRecoveryCode(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime? UsedAt { get; private set; }

    public static MfaRecoveryCode Create(Guid tenantId, Guid userId, string codeHash)
        => new(NewId(), tenantId) { UserId = userId, CodeHash = codeHash };

    public void Use(DateTime utcNow) => UsedAt = utcNow;
}
