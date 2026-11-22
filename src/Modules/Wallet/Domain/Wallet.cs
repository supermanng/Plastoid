using Plastoids.Modules.Wallet.Contracts;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Wallet.Domain;

/// <summary>
/// A prepaid, closed-loop unit balance for a person or an organisation (ADR 0018). Units are bought with money, spent on
/// courses, sessions and sponsored seats, refunded back as units, and never withdrawn as cash. The balance is the sum of
/// the open lots; it is cached here for reads and asserted equal on every change.
/// </summary>
[Audited]
public sealed class Wallet : Entity
{
    private Wallet()
    {
    }

    private Wallet(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string OwnerType { get; private set; } = string.Empty;
    public Guid OwnerId { get; private set; }
    public long Balance { get; private set; }
    public long LifetimePurchased { get; private set; }
    public long LifetimeRedeemed { get; private set; }

    public static Result<Wallet> Open(Guid tenantId, string ownerType, Guid ownerId)
    {
        if (!WalletOwnerTypes.IsValid(ownerType))
        {
            return Error.Validation("wallet_owner_type_invalid", "ownerType must be user or organisation.");
        }

        return new Wallet(NewId(), tenantId) { OwnerType = ownerType, OwnerId = ownerId };
    }

    internal void ApplyCredit(long units, string kind)
    {
        Balance += units;
        if (kind == WalletTransactionKinds.Purchase)
        {
            LifetimePurchased += units;
        }
    }

    internal void ApplyDebit(long units)
    {
        Balance -= units;
        LifetimeRedeemed += units;
    }
}

/// <summary>
/// Units bought together at one price. Redemptions consume lots first-in, first-out and release the naira the units were
/// bought for, so the unit liability in the ledger is always backed by exactly the cash collected (no revaluation when the
/// unit price changes later).
/// </summary>
public sealed class UnitLot : Entity
{
    private UnitLot()
    {
    }

    private UnitLot(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid WalletId { get; private set; }
    public long UnitsTotal { get; private set; }
    public long UnitsRemaining { get; private set; }
    public Money ValueTotal { get; private set; }
    public Money ValueRemaining { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string Reference { get; private set; } = string.Empty;
    public DateTime AcquiredAt { get; private set; }

    public bool IsOpen => UnitsRemaining > 0;

    public static UnitLot Create(Guid tenantId, Guid walletId, long units, Money value, string kind, string reference, DateTime utcNow)
        => new(NewId(), tenantId) { WalletId = walletId, UnitsTotal = units, UnitsRemaining = units, ValueTotal = value, ValueRemaining = value, Kind = kind, Reference = reference, AcquiredAt = utcNow };

    /// <summary>Takes up to <paramref name="units"/> from this lot; returns the units taken and their pro-rata naira value (the last unit takes any rounding residue).</summary>
    internal (long Units, Money Value) Consume(long units)
    {
        var take = Math.Min(units, UnitsRemaining);
        if (take <= 0)
        {
            return (0, Money.Zero(ValueRemaining.Currency));
        }

        var valueKobo = take == UnitsRemaining
            ? ValueRemaining.AmountKobo
            : (long)Math.Round((decimal)ValueRemaining.AmountKobo * take / UnitsRemaining, 0, MidpointRounding.ToEven);
        var value = new Money(valueKobo, ValueRemaining.Currency);
        UnitsRemaining -= take;
        ValueRemaining -= value;
        return (take, value);
    }
}

/// <summary>Every movement on a wallet, append-only. Units are signed (credits positive, redemptions negative).</summary>
public sealed class WalletTransaction : Entity
{
    private WalletTransaction()
    {
    }

    private WalletTransaction(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid WalletId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public long Units { get; private set; }
    public Money Value { get; private set; }
    public long BalanceAfter { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public Guid? ReferenceId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid? ActorId { get; private set; }
    public DateTime OccurredAt { get; private set; }

    public static WalletTransaction Record(Guid tenantId, Guid walletId, string kind, long units, Money value, long balanceAfter, string reference, string purpose, Guid? referenceId, string description, Guid? actorId, DateTime utcNow)
        => new(NewId(), tenantId)
        {
            WalletId = walletId,
            Kind = kind,
            Units = units,
            Value = value,
            BalanceAfter = balanceAfter,
            Reference = reference,
            Purpose = purpose,
            ReferenceId = referenceId,
            Description = description.Trim(),
            ActorId = actorId,
            OccurredAt = utcNow,
        };
}

public enum UnitPurchaseStatus
{
    Pending = 0,
    Credited = 1,
    Failed = 2,
}

/// <summary>A unit purchase in flight: the units and price are fixed when checkout starts, so a later rate change never alters what the buyer gets.</summary>
public sealed class UnitPurchase : Entity
{
    private UnitPurchase()
    {
    }

    private UnitPurchase(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid WalletId { get; private set; }
    public Guid BuyerUserId { get; private set; }
    public long Units { get; private set; }
    public long RateKobo { get; private set; }
    public Money Amount { get; private set; }
    public Guid PaymentIntentId { get; private set; }
    public UnitPurchaseStatus Status { get; private set; }
    public DateTime? CreditedAt { get; private set; }
    public DateTime? FailedAt { get; private set; }

    public static UnitPurchase Start(Guid tenantId, Guid walletId, Guid buyerUserId, long units, long rateKobo, Guid paymentIntentId)
        => new(NewId(), tenantId) { WalletId = walletId, BuyerUserId = buyerUserId, Units = units, RateKobo = rateKobo, Amount = new Money(checked(units * rateKobo), Money.Naira), PaymentIntentId = paymentIntentId, Status = UnitPurchaseStatus.Pending };

    public bool MarkCredited(DateTime utcNow)
    {
        if (Status == UnitPurchaseStatus.Credited)
        {
            return false;
        }

        Status = UnitPurchaseStatus.Credited;
        CreditedAt = utcNow;
        return true;
    }

    public void MarkFailed(DateTime utcNow)
    {
        if (Status == UnitPurchaseStatus.Pending)
        {
            Status = UnitPurchaseStatus.Failed;
            FailedAt = utcNow;
        }
    }
}
