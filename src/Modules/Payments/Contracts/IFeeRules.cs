namespace Plastoids.Modules.Payments.Contracts;

public static class PaymentPurposes
{
    public const string Course = "course";
    public const string Booking = "booking";
    public const string CoPay = "co_pay";

    /// <summary>Buying units for a wallet (ADR 0018): no commission, no payee; the cash becomes a unit liability.</summary>
    public const string Units = "units";
    public static readonly IReadOnlyCollection<string> All = [Course, Booking, CoPay, Units];
    public static bool IsValid(string p) => All.Contains(p, StringComparer.Ordinal);
}

public static class LearnerSources
{
    public const string Platform = "platform";
    public const string TutorReferred = "tutor_referred";
}

/// <summary>Inputs the fee rules look at (blueprint §18: source of learner, tutor plan, promotion).</summary>
public sealed record SaleContext(string Purpose, string LearnerSource, string? TutorPlan = null);

/// <summary>Commission in basis points plus the rule that produced it; stored on the transaction row so later rule changes never alter past earnings.</summary>
public sealed record FeeDecision(int CommissionBps, string RuleCode);

public interface IFeeRules
{
    FeeDecision Resolve(SaleContext context);
}
