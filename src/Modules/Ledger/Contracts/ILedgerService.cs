using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Ledger.Contracts;

/// <summary>Chart of accounts (blueprint §18). Codes are the only identifiers other modules use.</summary>
public static class AccountCodes
{
    public const string BankOperating = "1010";
    public const string BankClientFunds = "1020";
    public const string GatewayReceivable = "1030";
    public const string TutorPayable = "2010";
    public const string ProgrammeFundsHeld = "2020";
    public const string LearnerRefundCredit = "2030";
    public const string VatPayable = "2040";
    public const string CourseCommission = "4010";
    public const string TutoringCommission = "4020";
    public const string ProgrammeManagementFee = "4030";
    public const string PlatformSeatFee = "4040";
    public const string Subscriptions = "4050";
    public const string ProcessingFees = "5010";

    public static readonly IReadOnlyList<string> All = [BankOperating, BankClientFunds, GatewayReceivable, TutorPayable, ProgrammeFundsHeld, LearnerRefundCredit, VatPayable, CourseCommission, TutoringCommission, ProgrammeManagementFee, PlatformSeatFee, Subscriptions, ProcessingFees];
}

/// <summary>Sub-ledger owners: 2010 per tutor, 2020 per programme, 2030 per learner.</summary>
public static class SubLedgerOwnerTypes
{
    public const string Tutor = "tutor";
    public const string Programme = "programme";
    public const string Learner = "learner";
}

/// <summary>One side of a posting. Exactly one of Debit/Credit is non-zero.</summary>
public sealed record PostingDraft(string AccountCode, Money Debit, Money Credit, string? OwnerType = null, Guid? OwnerId = null)
{
    public static PostingDraft Dr(string accountCode, Money amount, string? ownerType = null, Guid? ownerId = null) => new(accountCode, amount, Money.Zero(amount.Currency), ownerType, ownerId);
    public static PostingDraft Cr(string accountCode, Money amount, string? ownerType = null, Guid? ownerId = null) => new(accountCode, Money.Zero(amount.Currency), amount, ownerType, ownerId);
}

/// <summary>A balanced journal entry to post. Reference must be unique (idempotency: the same reference posts once).</summary>
public sealed record JournalEntryDraft(string Reference, string Kind, string Description, IReadOnlyList<PostingDraft> Postings, DateTime? OccurredAt = null);

/// <summary>The only write path into the ledger (build prompt §2.9). Joins the caller's unit of work.</summary>
public interface ILedgerService
{
    /// <summary>Validates (balanced, known accounts, non-negative), then adds the entry to the ambient transaction. Returns the entry id; a repeated reference returns the existing id.</summary>
    Task<Result<Guid>> PostAsync(JournalEntryDraft draft, CancellationToken cancellationToken);

    /// <summary>Natural balance: assets/expenses = debits − credits; liabilities/revenue = credits − debits. OwnerId narrows to a sub-ledger.</summary>
    Task<Money> GetBalanceAsync(string accountCode, Guid? ownerId, CancellationToken cancellationToken);
}

/// <summary>Reconciliation outcome consumed by Payouts (blueprint §18: shortfall freezes payouts).</summary>
public interface IPayoutControls
{
    Task<bool> ArePayoutsFrozenAsync(CancellationToken cancellationToken);
}
