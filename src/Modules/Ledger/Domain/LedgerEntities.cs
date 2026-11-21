using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Ledger.Domain;

public enum AccountKind
{
    Asset = 0,
    Liability = 1,
    Revenue = 2,
    Expense = 3,
}

/// <summary>Chart of accounts row (seeded by migration).</summary>
public sealed class Account : Entity
{
    private Account()
    {
    }

    private Account(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public AccountKind Kind { get; private set; }
    public string? SubLedgerOwnerType { get; private set; }

    public static Account Create(Guid tenantId, string code, string name, AccountKind kind, string? subLedgerOwnerType)
        => new(NewId(), tenantId) { Code = code, Name = name, Kind = kind, SubLedgerOwnerType = subLedgerOwnerType };

    /// <summary>Natural balance sign: +1 when debits increase the balance.</summary>
    public int DebitSign => Kind is AccountKind.Asset or AccountKind.Expense ? 1 : -1;
}

/// <summary>Append-only (triggers). Postings sum to zero (checked in code and by the nightly job).</summary>
public sealed class JournalEntry : Entity
{
    private JournalEntry()
    {
    }

    private JournalEntry(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string Reference { get; private set; } = string.Empty;
    public string Kind { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }
    public Guid? PostedBy { get; private set; }
    public string Currency { get; private set; } = Money.Naira;
    public long TotalDebitKobo { get; private set; }

    public static JournalEntry Create(Guid tenantId, string reference, string kind, string description, DateTime occurredAt, Guid? postedBy, string currency, long totalDebitKobo)
        => new(NewId(), tenantId) { Reference = reference, Kind = kind, Description = description, OccurredAt = occurredAt, PostedBy = postedBy, Currency = currency, TotalDebitKobo = totalDebitKobo };
}

public sealed class Posting : Entity
{
    private Posting()
    {
    }

    private Posting(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid JournalEntryId { get; private set; }
    public string AccountCode { get; private set; } = string.Empty;
    public string? OwnerType { get; private set; }
    public Guid? OwnerId { get; private set; }
    public long DebitKobo { get; private set; }
    public long CreditKobo { get; private set; }
    public string Currency { get; private set; } = Money.Naira;
    public int Line { get; private set; }

    public static Posting Create(Guid tenantId, Guid entryId, int line, string accountCode, string? ownerType, Guid? ownerId, Money debit, Money credit)
        => new(NewId(), tenantId) { JournalEntryId = entryId, Line = line, AccountCode = accountCode, OwnerType = ownerType, OwnerId = ownerId, DebitKobo = debit.AmountKobo, CreditKobo = credit.AmountKobo, Currency = debit.Currency };
}

/// <summary>Single-row control table: payouts_frozen plus the last reconciliation outcome.</summary>
public sealed class LedgerControl : Entity
{
    public static readonly Guid SingletonId = new("00000000-0000-7000-8000-00000000c0de");

    private LedgerControl()
    {
    }

    private LedgerControl(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public bool PayoutsFrozen { get; private set; }
    public string? FreezeReason { get; private set; }
    public DateTime? LastReconciledAt { get; private set; }
    public long? LastAssetsKobo { get; private set; }
    public long? LastLiabilitiesKobo { get; private set; }
    public DateTime? LastVerifiedAt { get; private set; }
    public int LastUnbalancedEntries { get; private set; }

    public static LedgerControl Create(Guid tenantId) => new(SingletonId, tenantId);

    public void RecordReconciliation(DateTime utcNow, long assetsKobo, long liabilitiesKobo)
    {
        LastReconciledAt = utcNow;
        LastAssetsKobo = assetsKobo;
        LastLiabilitiesKobo = liabilitiesKobo;
        if (assetsKobo < liabilitiesKobo)
        {
            PayoutsFrozen = true;
            FreezeReason = $"Shortfall of {(liabilitiesKobo - assetsKobo) / 100m:N2} on {utcNow:yyyy-MM-dd}";
        }
    }

    public void RecordVerification(DateTime utcNow, int unbalanced)
    {
        LastVerifiedAt = utcNow;
        LastUnbalancedEntries = unbalanced;
    }

    /// <summary>Finance lifts the freeze after the shortfall is explained (staff endpoint, Sprint 8).</summary>
    public void Unfreeze()
    {
        PayoutsFrozen = false;
        FreezeReason = null;
    }
}
