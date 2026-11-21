using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Payments.Domain;

public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Expired = 3,
}

/// <summary>Blueprint §16 payments.PaymentIntent. gateway_reference is unique so retries and duplicate webhooks cannot double-post.</summary>
[Audited]
public sealed class PaymentIntent : Entity
{
    private PaymentIntent()
    {
    }

    private PaymentIntent(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid PayerId { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public Guid ReferenceId { get; private set; }
    public string? QuoteId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Money Amount { get; private set; }
    public string Gateway { get; private set; } = string.Empty;
    public string GatewayReference { get; private set; } = string.Empty;
    public string CheckoutUrl { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public Guid? PayeeUserId { get; private set; }
    public int CommissionBps { get; private set; }
    public string CommissionRuleCode { get; private set; } = string.Empty;
    public Money Commission { get; private set; }
    public Money PayeeNet { get; private set; }
    public Money GatewayFee { get; private set; }
    public DateTime? SucceededAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime? SettledAt { get; private set; }
    public DateTime? SweptAt { get; private set; }
    public Money RefundedAmount { get; private set; }
    public Guid? LedgerEntryId { get; private set; }

    public static PaymentIntent Create(Guid tenantId, Guid payerId, string purpose, Guid referenceId, string? quoteId, string description, Money amount, Guid? payeeUserId, int commissionBps, string ruleCode)
    {
        var commission = amount.Percent(commissionBps / 100m, MidpointRounding.ToEven);
        return new PaymentIntent(NewId(), tenantId)
        {
            PayerId = payerId,
            Purpose = purpose,
            ReferenceId = referenceId,
            QuoteId = quoteId,
            Description = description,
            Amount = amount,
            Status = PaymentStatus.Pending,
            PayeeUserId = payeeUserId,
            CommissionBps = commissionBps,
            CommissionRuleCode = ruleCode,
            Commission = commission,
            PayeeNet = amount - commission,
            GatewayFee = Money.Zero(amount.Currency),
            RefundedAmount = Money.Zero(amount.Currency),
        };
    }

    public void AttachCheckout(string gateway, string gatewayReference, string checkoutUrl)
    {
        Gateway = gateway;
        GatewayReference = gatewayReference;
        CheckoutUrl = checkoutUrl;
    }

    /// <summary>Returns true the first time; later calls (duplicate webhook, verify after webhook) are no-ops.</summary>
    public bool MarkSucceeded(Money gatewayFee, DateTime utcNow, Guid ledgerEntryId)
    {
        if (Status == PaymentStatus.Succeeded)
        {
            return false;
        }

        Status = PaymentStatus.Succeeded;
        GatewayFee = gatewayFee;
        SucceededAt = utcNow;
        LedgerEntryId = ledgerEntryId;
        FailedAt = null;
        FailureReason = null;
        return true;
    }

    public bool MarkFailed(string reason, DateTime utcNow)
    {
        if (Status != PaymentStatus.Pending)
        {
            return false;
        }

        Status = PaymentStatus.Failed;
        FailedAt = utcNow;
        FailureReason = reason;
        return true;
    }

    public Result MarkSettled(Money gatewayFee, DateTime utcNow)
    {
        if (Status != PaymentStatus.Succeeded)
        {
            return Error.Conflict("payment_not_succeeded", "Only successful payments settle.");
        }

        if (SettledAt is not null)
        {
            return Error.Conflict("payment_already_settled", "Already settled.");
        }

        GatewayFee = gatewayFee;
        SettledAt = utcNow;
        return Result.Success();
    }

    public void MarkSwept(DateTime utcNow) => SweptAt = utcNow;

    public Money RefundableAmount => Amount - RefundedAmount;

    public void ApplyRefund(Money amount) => RefundedAmount += amount;

    /// <summary>Commission earned less the gateway fee: what the sweep moves to operating (blueprint entry 3).</summary>
    public Money SweepableCommission => Commission - GatewayFee;
}
