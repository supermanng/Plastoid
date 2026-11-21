using System.Text.RegularExpressions;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.BackOffice.Domain;

/// <summary>Metric keys of the event-derived dashboard projection (blueprint §9.20: never recomputed on screen).</summary>
public static class Metrics
{
    public const string Gmv = "gmv";
    public const string Refunds = "refunds";
    public const string LearnersRegistered = "learners_registered";
    public const string TutorsVerified = "tutors_verified";
    public const string SponsorsVerified = "sponsors_verified";
    public const string EnrolmentsCreated = "enrolments_created";
    public const string EnrolmentsCompleted = "enrolments_completed";
    public const string ProgrammesFunded = "programmes_funded";
    public const string ProgrammesPublished = "programmes_published";
    public const string PayoutBatchesApproved = "payout_batches_approved";
    public const string PayoutsSettled = "payouts_settled";
    public const string PayoutsFailed = "payouts_failed";
    public const string TutorStrikes = "tutor_strikes";
}

/// <summary>A counter plus a money total per metric, bumped by consumers.</summary>
public sealed class MetricCounter : Entity
{
    private MetricCounter()
    {
    }

    private MetricCounter(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string Key { get; private set; } = string.Empty;
    public long Count { get; private set; }
    public Money Amount { get; private set; } = Money.Zero();
    public DateTime LastEventAt { get; private set; }

    public static MetricCounter Start(Guid tenantId, string key) => new(NewId(), tenantId) { Key = key };

    public void Bump(Money? amount, DateTime utcNow)
    {
        Count++;
        if (amount is { } a)
        {
            Amount += a;
        }

        LastEventAt = utcNow;
    }
}

public static class AlertKinds
{
    public const string Shortfall = "shortfall";
    public const string TutorStrikes = "tutor_strikes";
    public const string PayoutFailed = "payout_failed";
}

/// <summary>Risk alert raised from events; resolved by staff.</summary>
[Audited]
public sealed class RiskAlert : Entity
{
    private RiskAlert()
    {
    }

    private RiskAlert(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string Kind { get; private set; } = string.Empty;
    public string Severity { get; private set; } = "high";
    public string Message { get; private set; } = string.Empty;
    public Guid? ReferenceId { get; private set; }
    public DateTime RaisedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public string? Resolution { get; private set; }

    public bool IsOpen => ResolvedAt is null;

    public static RiskAlert Raise(Guid tenantId, string kind, string severity, string message, Guid? referenceId, DateTime utcNow)
        => new(NewId(), tenantId) { Kind = kind, Severity = severity, Message = message.Length > 500 ? message[..500] : message, ReferenceId = referenceId, RaisedAt = utcNow };

    public Result Resolve(Guid staffId, string resolution, DateTime utcNow)
    {
        if (!IsOpen)
        {
            return Error.Conflict("alert_already_resolved", "This alert is already resolved.");
        }

        ResolvedAt = utcNow;
        ResolvedBy = staffId;
        Resolution = resolution.Trim();
        return Result.Success();
    }
}

/// <summary>A feature flag: key (kebab-case), on/off, description. Audited on every change.</summary>
[Audited]
public sealed class FeatureFlag : Entity
{
    private static readonly Regex KeyPattern = new("^[a-z][a-z0-9-]{1,63}$", RegexOptions.Compiled);

    private FeatureFlag()
    {
    }

    private FeatureFlag(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string Key { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid UpdatedBy { get; private set; }

    public static bool IsValidKey(string key) => KeyPattern.IsMatch(key);

    public static Result<FeatureFlag> Create(Guid tenantId, string key, bool enabled, string description, Guid by)
    {
        if (!IsValidKey(key))
        {
            return Error.Validation("flag_key_invalid", "Flag keys are kebab-case: letters, digits and hyphens, 2–64 characters.");
        }

        return new FeatureFlag(NewId(), tenantId) { Key = key, Enabled = enabled, Description = description.Trim(), UpdatedBy = by };
    }

    public void Set(bool enabled, string? description, Guid by)
    {
        Enabled = enabled;
        if (description is not null)
        {
            Description = description.Trim();
        }

        UpdatedBy = by;
    }
}
