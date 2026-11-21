using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Payouts.Domain;

public static class EarningSources
{
    public const string Course = "course";
    public const string Booking = "booking";
    public const string Programme = "programme";
}

public enum EarningStatus
{
    Held = 0,
    Available = 1,
}

/// <summary>
/// One tutor earning with its settlement hold (blueprint §18): course sales 7 days (refund window), sessions until Booking's
/// 72-hour hold releases, sponsored teaching per delivered programme month. Net = gross − commission, less refunds.
/// </summary>
public sealed class Earning : Entity
{
    private Earning()
    {
    }

    private Earning(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid TutorUserId { get; private set; }
    public string SourceType { get; private set; } = string.Empty;
    public Guid SourceId { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string SourceKey { get; private set; } = string.Empty;
    public Money Gross { get; private set; }
    public Money Commission { get; private set; }
    public Money Net { get; private set; }
    public Money RefundedNet { get; private set; }
    public DateTime EarnedAt { get; private set; }
    public DateTime? ReleasesAt { get; private set; }
    public DateTime? ReleasedAt { get; private set; }
    public EarningStatus Status { get; private set; }

    public static Earning Record(Guid tenantId, Guid tutorUserId, string sourceType, Guid sourceId, Guid? referenceId, string sourceKey, Money gross, Money net, DateTime earnedAt, DateTime? releasesAt)
        => new(NewId(), tenantId)
        {
            TutorUserId = tutorUserId,
            SourceType = sourceType,
            SourceId = sourceId,
            ReferenceId = referenceId,
            SourceKey = sourceKey,
            Gross = gross,
            Net = net,
            Commission = gross - net,
            RefundedNet = Money.Zero(gross.Currency),
            EarnedAt = earnedAt,
            ReleasesAt = releasesAt,
            Status = EarningStatus.Held,
        };

    public Money PayableNet => Net - RefundedNet;

    public bool Release(DateTime utcNow)
    {
        if (Status == EarningStatus.Available)
        {
            return false;
        }

        Status = EarningStatus.Available;
        ReleasedAt = utcNow;
        return true;
    }

    /// <summary>A refund of part of the gross reduces the tutor's share pro rata (ledger entry 5 does the same on 2010).</summary>
    public void ApplyRefund(Money refundedGross)
    {
        if (Gross.IsZero || refundedGross.IsZero)
        {
            return;
        }

        var fraction = (decimal)refundedGross.AmountKobo / Gross.AmountKobo;
        var share = new Money((long)Math.Round(Net.AmountKobo * fraction, 0, MidpointRounding.ToEven), Net.Currency);
        RefundedNet = RefundedNet + share > Net ? Net : RefundedNet + share;
    }
}

/// <summary>A dispute (or manual) hold on a tutor: no payout while open; auto-releases after at most 5 working days (blueprint §18).</summary>
public sealed class DisputeHold : Entity
{
    public const int MaxWorkingDays = 5;

    private DisputeHold()
    {
    }

    private DisputeHold(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid TutorUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid PlacedBy { get; private set; }
    public DateTime PlacedAt { get; private set; }
    public DateTime ReleasesAt { get; private set; }
    public DateTime? ReleasedAt { get; private set; }
    public Guid? ReleasedBy { get; private set; }
    public string? Resolution { get; private set; }

    public bool IsOpen => ReleasedAt is null;

    public static DisputeHold Place(Guid tenantId, Guid tutorUserId, string reason, Guid placedBy, DateTime utcNow, DateTime releasesAt)
        => new(NewId(), tenantId) { TutorUserId = tutorUserId, Reason = reason.Trim(), PlacedBy = placedBy, PlacedAt = utcNow, ReleasesAt = releasesAt };

    public Result Release(Guid releasedBy, string resolution, DateTime utcNow)
    {
        if (!IsOpen)
        {
            return Error.Conflict("hold_already_released", "This hold is already released.");
        }

        ReleasedAt = utcNow;
        ReleasedBy = releasedBy;
        Resolution = resolution.Trim();
        return Result.Success();
    }
}

public enum BatchStatus
{
    PendingApproval = 0,
    Approved = 1,
    Submitted = 2,
    Completed = 3,
    Rejected = 4,
}

/// <summary>A payout run (weekly, Thursdays). Maker–checker: the approver can never be the creator — enforced here, not in the UI.</summary>
[Audited]
public sealed class PayoutBatch : Entity
{
    private PayoutBatch()
    {
    }

    private PayoutBatch(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string Reference { get; private set; } = string.Empty;
    public BatchStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public Guid? RejectedBy { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? Provider { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int Count { get; private set; }
    public Money Total { get; private set; } = Money.Zero();

    public static PayoutBatch Create(Guid tenantId, string reference, Guid createdBy, int count, Money total)
        => new(NewId(), tenantId) { Reference = reference, CreatedBy = createdBy, Count = count, Total = total, Status = BatchStatus.PendingApproval };

    public Result Approve(Guid checker, DateTime utcNow)
    {
        if (Status != BatchStatus.PendingApproval)
        {
            return Error.Conflict("batch_not_pending", $"The batch is {Status.ToString().ToLowerInvariant()}.");
        }

        if (checker == CreatedBy)
        {
            return Error.Forbidden("maker_checker_violation", "The person who created the batch cannot approve it.");
        }

        Status = BatchStatus.Approved;
        ApprovedBy = checker;
        ApprovedAt = utcNow;
        return Result.Success();
    }

    public Result Reject(Guid by, string reason)
    {
        if (Status != BatchStatus.PendingApproval)
        {
            return Error.Conflict("batch_not_pending", $"The batch is {Status.ToString().ToLowerInvariant()}.");
        }

        Status = BatchStatus.Rejected;
        RejectedBy = by;
        RejectionReason = reason.Trim();
        return Result.Success();
    }

    public Result MarkSubmitted(string provider, DateTime utcNow)
    {
        if (Status != BatchStatus.Approved)
        {
            return Error.Conflict("batch_not_approved", "Only approved batches can be submitted.");
        }

        Status = BatchStatus.Submitted;
        Provider = provider;
        SubmittedAt = utcNow;
        return Result.Success();
    }

    public bool TryComplete(IEnumerable<Payout> payouts, DateTime utcNow)
    {
        if (Status != BatchStatus.Submitted || payouts.Any(p => p.Status is PayoutStatus.Pending or PayoutStatus.Submitted))
        {
            return false;
        }

        Status = BatchStatus.Completed;
        CompletedAt = utcNow;
        return true;
    }
}

public enum PayoutStatus
{
    Pending = 0,
    Submitted = 1,
    Settled = 2,
    Failed = 3,
    Reversed = 4,
    Cancelled = 5,
}

/// <summary>One transfer to one tutor inside a batch.</summary>
public sealed class Payout : Entity
{
    private Payout()
    {
    }

    private Payout(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid BatchId { get; private set; }
    public Guid TutorUserId { get; private set; }
    public Money Amount { get; private set; }
    public string BankCode { get; private set; } = string.Empty;
    public string AccountNumberMasked { get; private set; } = string.Empty;
    public string AccountName { get; private set; } = string.Empty;
    public PayoutStatus Status { get; private set; }
    public string? ProviderReference { get; private set; }
    public string? BankReference { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? SettledAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public Guid? LedgerEntryId { get; private set; }
    public Guid? ReversalEntryId { get; private set; }

    public static Payout Create(Guid tenantId, Guid batchId, Guid tutorUserId, Money amount, string bankCode, string accountNumberMasked, string accountName)
        => new(NewId(), tenantId) { BatchId = batchId, TutorUserId = tutorUserId, Amount = amount, BankCode = bankCode, AccountNumberMasked = accountNumberMasked, AccountName = accountName, Status = PayoutStatus.Pending };

    public bool CountsAsPending => Status is PayoutStatus.Pending or PayoutStatus.Submitted;

    public void MarkSubmitted(string providerReference, DateTime utcNow)
    {
        ProviderReference = providerReference;
        SubmittedAt = utcNow;
        Status = PayoutStatus.Submitted;
    }

    public Result Settle(string bankReference, Guid ledgerEntryId, DateTime utcNow)
    {
        if (Status == PayoutStatus.Settled)
        {
            return Result.Success();
        }

        if (Status != PayoutStatus.Submitted)
        {
            return Error.Conflict("payout_not_submitted", $"The payout is {Status.ToString().ToLowerInvariant()}.");
        }

        Status = PayoutStatus.Settled;
        BankReference = bankReference;
        LedgerEntryId = ledgerEntryId;
        SettledAt = utcNow;
        return Result.Success();
    }

    public Result Fail(string reason, DateTime utcNow)
    {
        if (Status is not (PayoutStatus.Pending or PayoutStatus.Submitted))
        {
            return Error.Conflict("payout_not_open", $"The payout is {Status.ToString().ToLowerInvariant()}.");
        }

        Status = PayoutStatus.Failed;
        FailureReason = reason.Trim();
        FailedAt = utcNow;
        return Result.Success();
    }

    /// <summary>A settled transfer bounced back (closed account, bank reversal): the money returns to the tutor's balance.</summary>
    public Result Reverse(string reason, Guid reversalEntryId, DateTime utcNow)
    {
        if (Status != PayoutStatus.Settled)
        {
            return Error.Conflict("payout_not_settled", "Only settled payouts can be reversed.");
        }

        Status = PayoutStatus.Reversed;
        FailureReason = reason.Trim();
        ReversalEntryId = reversalEntryId;
        FailedAt = utcNow;
        return Result.Success();
    }

    public void Cancel()
    {
        if (Status == PayoutStatus.Pending)
        {
            Status = PayoutStatus.Cancelled;
        }
    }
}

/// <summary>Provider webhook ids already processed (replay protection).</summary>
public sealed class ProviderEvent : Entity
{
    private ProviderEvent()
    {
    }

    private ProviderEvent(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string Provider { get; private set; } = string.Empty;
    public string EventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string Reference { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public DateTime ReceivedAt { get; private set; }

    public static ProviderEvent Record(Guid tenantId, string provider, string eventId, string eventType, string reference, string outcome, DateTime utcNow)
        => new(NewId(), tenantId) { Provider = provider, EventId = eventId, EventType = eventType, Reference = reference, Outcome = outcome, ReceivedAt = utcNow };
}
