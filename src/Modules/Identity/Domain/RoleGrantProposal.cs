using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Identity.Domain;

public enum RoleGrantStatus
{
    Proposed = 0,
    Approved = 1,
    Rejected = 2,
}

/// <summary>Four-eyes platform role grant (blueprint §19 controls): one staff member proposes, a different one approves. Grantable: staff, tutor, sponsor.</summary>
[Audited]
public sealed class RoleGrantProposal : Entity
{
    public static readonly IReadOnlyCollection<string> GrantableRoles = ["staff", "tutor", "sponsor"];

    private RoleGrantProposal()
    {
    }

    private RoleGrantProposal(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid UserId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public RoleGrantStatus Status { get; private set; }
    public Guid ProposedBy { get; private set; }
    public DateTime ProposedAt { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }

    public static Result<RoleGrantProposal> Propose(Guid tenantId, Guid userId, string role, string reason, Guid proposedBy, DateTime utcNow)
    {
        var r = role.Trim().ToLowerInvariant();
        if (!GrantableRoles.Contains(r))
        {
            return Error.Validation("role_not_grantable", "Role must be staff, tutor or sponsor.");
        }

        if (userId == proposedBy && r == "staff")
        {
            return Error.Forbidden("self_grant", "Staff cannot propose a role for themselves.");
        }

        return new RoleGrantProposal(NewId(), tenantId) { UserId = userId, Role = r, Reason = reason.Trim(), ProposedBy = proposedBy, ProposedAt = utcNow, Status = RoleGrantStatus.Proposed };
    }

    public Result Approve(Guid approver, DateTime utcNow)
    {
        if (Status != RoleGrantStatus.Proposed)
        {
            return Error.Conflict("grant_not_pending", $"The proposal is {Status.ToString().ToLowerInvariant()}.");
        }

        if (approver == ProposedBy)
        {
            return Error.Forbidden("maker_checker_violation", "The proposer cannot approve their own role grant.");
        }

        if (approver == UserId)
        {
            return Error.Forbidden("self_approval", "The beneficiary cannot approve the grant.");
        }

        Status = RoleGrantStatus.Approved;
        DecidedBy = approver;
        DecidedAt = utcNow;
        return Result.Success();
    }

    public Result Reject(Guid by, string note, DateTime utcNow)
    {
        if (Status != RoleGrantStatus.Proposed)
        {
            return Error.Conflict("grant_not_pending", $"The proposal is {Status.ToString().ToLowerInvariant()}.");
        }

        Status = RoleGrantStatus.Rejected;
        DecidedBy = by;
        DecidedAt = utcNow;
        DecisionNote = note.Trim();
        return Result.Success();
    }
}
