using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Payments.Contracts;

/// <summary>What a module needs to take a payment for its own aggregate (booking, package, co-pay).</summary>
public sealed record IntentRequest(Guid PayerId, string Purpose, Guid ReferenceId, Money Amount, Guid? PayeeUserId, string Description, string LearnerSource = LearnerSources.Platform, bool PayWithUnits = false);

/// <summary>CheckoutUrl is null when the intent was settled from the payer's unit wallet (Status "succeeded", UnitsCharged &gt; 0).</summary>
public sealed record IntentHandle(Guid PaymentIntentId, string GatewayReference, Uri? CheckoutUrl, Money Amount, int CommissionBps, string Status = "pending", long UnitsCharged = 0);

public static class RefundDestinations
{
    /// <summary>Back to the original payment method through the gateway.</summary>
    public const string Card = "card";

    /// <summary>To the learner's refund credit (ledger 2030), spendable only on the platform (blueprint §18: no stored value).</summary>
    public const string Credit = "credit";

    /// <summary>Back to the wallet the units came from (ADR 0018). The only destination for a payment made with units.</summary>
    public const string Units = "units";
}

/// <summary>A policy-driven refund (cancellation rules). Issued automatically: no maker–checker, the rule is the approval.</summary>
public sealed record PolicyRefundRequest(Guid PaymentIntentId, Money Amount, string Reason, string Destination, Guid LearnerId);

public sealed record RefundHandle(Guid RefundId, Money Amount, string Destination);

/// <summary>In-process payment operations for other modules. Joins the caller's unit of work.</summary>
public interface IPaymentOperations
{
    Task<Result<IntentHandle>> CreateIntentAsync(IntentRequest request, CancellationToken cancellationToken);
    Task<Result<RefundHandle>> RefundByPolicyAsync(PolicyRefundRequest request, CancellationToken cancellationToken);
    Task<Money> GetLearnerCreditAsync(Guid learnerId, CancellationToken cancellationToken);
}
