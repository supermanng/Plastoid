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

/// <summary>Who a sale pays (ADR 0019).</summary>
public static class PayeeTypes
{
    public const string Tutor = "tutor";
    public const string Organisation = "organisation";
}

public static class LearnerSources
{
    public const string Platform = "platform";
    public const string TutorReferred = "tutor_referred";
}

/// <summary>Inputs the fee rules look at (blueprint §18: source of learner, tutor plan, promotion).</summary>
/// <summary>ProviderSale: the course belongs to an organisation (fee code course_provider, ADR 0019).</summary>
public sealed record SaleContext(string Purpose, string LearnerSource, string? TutorPlan = null, bool ProviderSale = false);

/// <summary>Commission in basis points plus the rule that produced it; stored on the transaction row so later rule changes never alter past earnings.</summary>
public sealed record FeeDecision(int CommissionBps, string RuleCode);

public interface IFeeRules
{
    FeeDecision Resolve(SaleContext context);
}
