using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Payments.Domain;

public enum FeeRuleStatus
{
    Proposed = 0,
    Approved = 1,
    Rejected = 2,
}

/// <summary>
/// Versioned fee configuration (blueprint §18 fee rules, §19 four-eyes): a rule for a code (course_platform, course_tutor_referred,
/// live_tutoring, co_pay) with a commission in basis points, effective from a date, proposed by one staff member and approved by another.
/// </summary>
[Audited]
public sealed class FeeRule : Entity
{
    public static readonly IReadOnlyCollection<string> Codes = ["course_platform", "course_tutor_referred", "live_tutoring", "co_pay"];

    private FeeRule()
    {
    }

    private FeeRule(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string RuleCode { get; private set; } = string.Empty;
    public int CommissionBps { get; private set; }
    public int Version { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public FeeRuleStatus Status { get; private set; }
    public string Rationale { get; private set; } = string.Empty;
    public Guid ProposedBy { get; private set; }
    public DateTime ProposedAt { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }

    public static Result<FeeRule> Propose(Guid tenantId, string ruleCode, int commissionBps, DateTime effectiveFrom, string rationale, int version, Guid proposedBy, DateTime utcNow)
    {
        if (!Codes.Contains(ruleCode))
        {
            return Error.Validation("fee_rule_code_unknown", "ruleCode must be one of " + string.Join(", ", Codes) + ".");
        }

        if (commissionBps is < 0 or > 5000)
        {
            return Error.Validation("commission_out_of_range", "Commission must be between 0 and 5,000 basis points (50 %).");
        }

        if (effectiveFrom < utcNow.AddMinutes(-5))
        {
            return Error.Validation("effective_from_past", "effectiveFrom cannot be in the past: past sales keep the rate stored on them.");
        }

        return new FeeRule(NewId(), tenantId) { RuleCode = ruleCode, CommissionBps = commissionBps, EffectiveFrom = effectiveFrom, Rationale = rationale.Trim(), Version = version, ProposedBy = proposedBy, ProposedAt = utcNow, Status = FeeRuleStatus.Proposed };
    }

    public Result Approve(Guid approver, DateTime utcNow)
    {
        if (Status != FeeRuleStatus.Proposed)
        {
            return Error.Conflict("fee_rule_not_pending", $"The rule is {Status.ToString().ToLowerInvariant()}.");
        }

        if (approver == ProposedBy)
        {
            return Error.Forbidden("maker_checker_violation", "The proposer cannot approve their own fee rule.");
        }

        Status = FeeRuleStatus.Approved;
        DecidedBy = approver;
        DecidedAt = utcNow;
        return Result.Success();
    }

    public Result Reject(Guid by, string note, DateTime utcNow)
    {
        if (Status != FeeRuleStatus.Proposed)
        {
            return Error.Conflict("fee_rule_not_pending", $"The rule is {Status.ToString().ToLowerInvariant()}.");
        }

        Status = FeeRuleStatus.Rejected;
        DecidedBy = by;
        DecidedAt = utcNow;
        DecisionNote = note.Trim();
        return Result.Success();
    }

    /// <summary>The approved rule in force at <paramref name="at"/>: latest effective-from not after the instant, highest version on ties.</summary>
    public static FeeRule? EffectiveAt(IEnumerable<FeeRule> rules, string ruleCode, DateTime at)
        => rules.Where(r => r.RuleCode == ruleCode && r.Status == FeeRuleStatus.Approved && r.EffectiveFrom <= at).OrderByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.Version).FirstOrDefault();
}
