using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Wallet.Domain;

public enum UnitRateStatus
{
    Proposed = 0,
    Approved = 1,
    Rejected = 2,
}

/// <summary>
/// The naira price of one unit, versioned and effective-from, proposed by one staff member and approved by another
/// (four-eyes, like fee rules). Purchases record the rate they used; a new rate never changes what was already bought.
/// </summary>
[Audited]
public sealed class UnitRate : Entity
{
    public const long MinRateKobo = 100;            // ₦1
    public const long MaxRateKobo = 100_000_000;    // ₦1,000,000

    private UnitRate()
    {
    }

    private UnitRate(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public long RateKobo { get; private set; }
    public int Version { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public UnitRateStatus Status { get; private set; }
    public string Rationale { get; private set; } = string.Empty;
    public Guid ProposedBy { get; private set; }
    public DateTime ProposedAt { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }

    public static Result<UnitRate> Propose(Guid tenantId, long rateKobo, DateTime effectiveFrom, string rationale, int version, Guid proposedBy, DateTime utcNow)
    {
        if (rateKobo is < MinRateKobo or > MaxRateKobo)
        {
            return Error.Validation("unit_rate_out_of_range", "A unit must be worth between ₦1 and ₦1,000,000.");
        }

        if (effectiveFrom < utcNow.AddMinutes(-5))
        {
            return Error.Validation("effective_from_past", "effectiveFrom cannot be in the past: past purchases keep the rate stored on them.");
        }

        if (string.IsNullOrWhiteSpace(rationale))
        {
            return Error.Validation("rationale_required", "Say why the unit price changes.");
        }

        return new UnitRate(NewId(), tenantId) { RateKobo = rateKobo, EffectiveFrom = effectiveFrom, Rationale = rationale.Trim(), Version = version, ProposedBy = proposedBy, ProposedAt = utcNow, Status = UnitRateStatus.Proposed };
    }

    public Result Approve(Guid approver, DateTime utcNow)
    {
        if (Status != UnitRateStatus.Proposed)
        {
            return Error.Conflict("unit_rate_not_pending", $"The rate is {Status.ToString().ToLowerInvariant()}.");
        }

        if (approver == ProposedBy)
        {
            return Error.Forbidden("maker_checker_violation", "The proposer cannot approve their own unit rate.");
        }

        Status = UnitRateStatus.Approved;
        DecidedBy = approver;
        DecidedAt = utcNow;
        return Result.Success();
    }

    public Result Reject(Guid by, string note, DateTime utcNow)
    {
        if (Status != UnitRateStatus.Proposed)
        {
            return Error.Conflict("unit_rate_not_pending", $"The rate is {Status.ToString().ToLowerInvariant()}.");
        }

        Status = UnitRateStatus.Rejected;
        DecidedBy = by;
        DecidedAt = utcNow;
        DecisionNote = note.Trim();
        return Result.Success();
    }

    /// <summary>The approved rate in force at <paramref name="at"/>: the latest effective-from not after that instant, highest version wins on a tie.</summary>
    public static UnitRate? EffectiveAt(IEnumerable<UnitRate> approved, DateTime at)
        => approved.Where(r => r.Status == UnitRateStatus.Approved && r.EffectiveFrom <= at).OrderByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.Version).FirstOrDefault();
}
