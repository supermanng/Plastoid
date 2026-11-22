using Plastoids.Modules.Payments.Contracts;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Payments.Domain;

public static class PaymentMethods
{
    public const string Gateway = "gateway";
    public const string Units = "units";
}

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

    /// <summary>"tutor" (PayeeUserId is paid PayeeNet) or "organisation" (ADR 0019: PayeeOrganisationId is paid PayeeNet, the engaged tutor PayeeUserId is paid TutorShare).</summary>
    public string PayeeType { get; private set; } = PayeeTypes.Tutor;
    public Guid? PayeeOrganisationId { get; private set; }

    /// <summary>The engaged tutor's part of a provider sale, computed from the engagement terms on the net of commission; zero for tutor sales.</summary>
    public Money TutorShare { get; private set; }
    public Guid? EngagementId { get; private set; }
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

    /// <summary>"gateway" (hosted checkout) or "units" (settled from a unit wallet, ADR 0018).</summary>
    public string PaymentMethod { get; private set; } = PaymentMethods.Gateway;
    public long UnitsCharged { get; private set; }
    public long UnitRateKobo { get; private set; }
    public long UnitsValueKobo { get; private set; }
    public Guid? UnitsWalletId { get; private set; }

    public bool PaidWithUnits => PaymentMethod == PaymentMethods.Units;

    /// <summary>A sale of an organisation-owned course (ADR 0019).</summary>
    public bool IsProviderSale => PayeeType == PayeeTypes.Organisation;

    /// <summary>What the sale leaves after commission: the tutor share plus the organisation's or tutor's net.</summary>
    public Money NetOfCommission => Amount - Commission;

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
            TutorShare = Money.Zero(amount.Currency),
            GatewayFee = Money.Zero(amount.Currency),
            RefundedAmount = Money.Zero(amount.Currency),
        };
    }

    /// <summary>
    /// A sale of an organisation-owned course (ADR 0019). Commission as for a tutor sale; <paramref name="tutorShare"/> is the engaged tutor's
    /// part (already computed on the net of commission and capped by the caller); the organisation receives the rest, never less than zero.
    /// </summary>
    public static PaymentIntent CreateForOrganisation(Guid tenantId, Guid payerId, string purpose, Guid referenceId, string? quoteId, string description, Money amount, Guid organisationId, Guid? tutorUserId, Guid? engagementId, Money tutorShare, int commissionBps, string ruleCode)
    {
        var commission = amount.Percent(commissionBps / 100m, MidpointRounding.ToEven);
        var net = amount - commission;
        var share = tutorUserId is null || tutorShare.IsNegative ? Money.Zero(amount.Currency) : (tutorShare > net ? net : tutorShare);
        return new PaymentIntent(NewId(), tenantId)
        {
            PayerId = payerId,
            Purpose = purpose,
            ReferenceId = referenceId,
            QuoteId = quoteId,
            Description = description,
            Amount = amount,
            Status = PaymentStatus.Pending,
            PayeeType = PayeeTypes.Organisation,
            PayeeOrganisationId = organisationId,
            PayeeUserId = tutorUserId,
            EngagementId = engagementId,
            CommissionBps = commissionBps,
            CommissionRuleCode = ruleCode,
            Commission = commission,
            TutorShare = share,
            PayeeNet = net - share,
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

    /// <summary>Settled from a wallet: no gateway, the "reference" is the wallet transaction. Value is the naira the units were bought for.</summary>
    public void AttachUnits(Guid walletId, long units, long rateKobo, long valueKobo)
    {
        PaymentMethod = PaymentMethods.Units;
        Gateway = PaymentMethods.Units;
        GatewayReference = $"units:{Id:N}";
        CheckoutUrl = string.Empty;
        UnitsWalletId = walletId;
        UnitsCharged = units;
        UnitRateKobo = rateKobo;
        UnitsValueKobo = valueKobo;
    }

    /// <summary>Units to hand back for a (partial) refund: pro rata to the amount, rounded half to even.</summary>
    public long UnitsToRefund(Money amount) => Amount.IsZero ? 0 : (long)Math.Round((decimal)UnitsCharged * amount.AmountKobo / Amount.AmountKobo, 0, MidpointRounding.ToEven);

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
