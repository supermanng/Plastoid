using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Identity.Domain;

public enum OrganisationType
{
    Sponsor = 0,
    Institution = 1,
    Employer = 2,
    Hub = 3,
}

public enum VerificationStatus
{
    Pending = 0,
    Verified = 1,
    Rejected = 2,
}

/// <summary>A sponsor, institution, employer or hub (blueprint §16). Verified by staff against CAC + authorised signatory.</summary>
[Audited]
public sealed class Organisation : Entity
{
    private Organisation()
    {
    }

    private Organisation(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string Name { get; private set; } = string.Empty;
    public OrganisationType Type { get; private set; }
    public string CacNumber { get; private set; } = string.Empty;
    public string SignatoryName { get; private set; } = string.Empty;
    public string SignatoryEmail { get; private set; } = string.Empty;
    public VerificationStatus VerificationStatus { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public Guid? VerifiedBy { get; private set; }
    public string? RejectionReason { get; private set; }

    public static Organisation Register(Guid tenantId, string name, OrganisationType type, string cacNumber, string signatoryName, string signatoryEmail)
        => new(NewId(), tenantId)
        {
            Name = name.Trim(),
            Type = type,
            CacNumber = cacNumber.Trim().ToUpperInvariant(),
            SignatoryName = signatoryName.Trim(),
            SignatoryEmail = signatoryEmail.Trim().ToLowerInvariant(),
            VerificationStatus = VerificationStatus.Pending,
        };

    /// <summary>Staff decision. Four-eyes: the verifier cannot be a member of the organisation (checked by the handler).</summary>
    public Result Verify(Guid staffUserId, DateTime utcNow)
    {
        if (VerificationStatus == VerificationStatus.Verified)
        {
            return Error.Conflict("organisation_already_verified", "Organisation is already verified.");
        }

        VerificationStatus = VerificationStatus.Verified;
        VerifiedAt = utcNow;
        VerifiedBy = staffUserId;
        RejectionReason = null;
        return Result.Success();
    }

    public Result Reject(Guid staffUserId, string reason, DateTime utcNow)
    {
        if (VerificationStatus == VerificationStatus.Verified)
        {
            return Error.Conflict("organisation_already_verified", "A verified organisation cannot be rejected; suspend it instead.");
        }

        VerificationStatus = VerificationStatus.Rejected;
        VerifiedAt = utcNow;
        VerifiedBy = staffUserId;
        RejectionReason = reason.Trim();
        return Result.Success();
    }

    public bool IsVerified => VerificationStatus == VerificationStatus.Verified;
}
