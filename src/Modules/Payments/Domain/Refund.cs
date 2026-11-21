using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Payments.Domain;

public enum RefundStatus
{
    PendingApproval = 0,
    Issued = 1,
    Rejected = 2,
}

/// <summary>Build prompt §2.10: refunds above ₦100,000 need approved_by != created_by (maker–checker), enforced here.</summary>
[Audited]
public sealed class Refund : Entity
{
    public static readonly Money FourEyesThreshold = Money.NairaAmount(100_000);

    private Refund()
    {
    }

    private Refund(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid PaymentIntentId { get; private set; }
    public Money Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public RefundStatus Status { get; private set; }
    public Guid RequestedBy { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? IssuedAt { get; private set; }
    public string? GatewayReference { get; private set; }
    public Guid? LedgerEntryId { get; private set; }

    public static Refund Request(Guid tenantId, Guid paymentIntentId, Money amount, string reason, Guid requestedBy)
        => new(NewId(), tenantId) { PaymentIntentId = paymentIntentId, Amount = amount, Reason = reason.Trim(), RequestedBy = requestedBy, Status = RefundStatus.PendingApproval, Destination = "card" };

    /// <summary>Cancellation-policy refunds are approved by the rule itself (blueprint §9: refund rules applied automatically).</summary>
    public static Refund ByPolicy(Guid tenantId, Guid paymentIntentId, Money amount, string reason, string destination, Guid? learnerId)
        => new(NewId(), tenantId) { PaymentIntentId = paymentIntentId, Amount = amount, Reason = reason.Trim(), RequestedBy = Guid.Empty, ApprovedBy = Guid.Empty, Status = RefundStatus.PendingApproval, PolicyDriven = true, Destination = destination, LearnerId = learnerId };

    /// <summary>"card" (gateway) or "credit" (learner refund credit, ledger 2030).</summary>
    public string Destination { get; private set; } = "card";

    public bool PolicyDriven { get; private set; }

    public Guid? LearnerId { get; private set; }

    public bool NeedsSecondApprover => !PolicyDriven && Amount > FourEyesThreshold;

    public Result Approve(Guid approverId)
    {
        if (Status != RefundStatus.PendingApproval)
        {
            return Error.Conflict("refund_not_pending", "This refund is not awaiting approval.");
        }

        if (NeedsSecondApprover && approverId == RequestedBy)
        {
            return Error.Forbidden("four_eyes_required", "Refunds above ₦100,000 must be approved by a different staff member.");
        }

        ApprovedBy = approverId;
        return Result.Success();
    }

    public void MarkIssued(string gatewayReference, Guid ledgerEntryId, DateTime utcNow)
    {
        Status = RefundStatus.Issued;
        GatewayReference = gatewayReference;
        LedgerEntryId = ledgerEntryId;
        IssuedAt = utcNow;
    }

    public void Reject() => Status = RefundStatus.Rejected;
}
