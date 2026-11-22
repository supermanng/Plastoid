using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Wallet.Contracts;

/// <summary>Who owns a wallet: a person (learner, tutor…) or an organisation (sponsor, provider).</summary>
public static class WalletOwnerTypes
{
    public const string User = "user";
    public const string Organisation = "organisation";
    public static readonly IReadOnlyCollection<string> All = [User, Organisation];
    public static bool IsValid(string t) => All.Contains(t, StringComparer.Ordinal);
}

/// <summary>The naira value of one unit, as approved by staff (four-eyes) and effective now.</summary>
public sealed record UnitRateSnapshot(long RateKobo, int Version, DateTime EffectiveFrom, string Source)
{
    public Money ValueOf(long units) => new(checked(units * RateKobo), Money.Naira);

    /// <summary>Whole units needed to cover an amount (rounded up: a learner never under-pays a price).</summary>
    public long UnitsFor(Money amount) => amount.AmountKobo <= 0 ? 0 : (amount.AmountKobo + RateKobo - 1) / RateKobo;
}

public interface IUnitRates
{
    Task<UnitRateSnapshot> CurrentAsync(CancellationToken cancellationToken);
}

/// <summary>Spend units. Reference is unique (idempotent: the same reference redeems once).</summary>
public sealed record UnitRedemptionRequest(string OwnerType, Guid OwnerId, long Units, string Reference, string Purpose, Guid? ReferenceId, string Description);

/// <summary>Units taken and the naira they were bought for (first-in, first-out across lots), which is what the ledger releases from the unit liability.</summary>
public sealed record UnitRedemption(Guid WalletId, Guid TransactionId, long Units, Money Value, long BalanceAfter);

/// <summary>Add units: a purchase (Kind purchase), a refund back to units, a close-out release or a staff adjustment.</summary>
public sealed record UnitCreditRequest(string OwnerType, Guid OwnerId, long Units, Money Value, string Reference, string Kind, string Purpose, Guid? ReferenceId, string Description);

public sealed record UnitCredit(Guid WalletId, Guid TransactionId, long Units, long BalanceAfter);

public static class WalletTransactionKinds
{
    public const string Purchase = "purchase";
    public const string Redemption = "redemption";
    public const string Refund = "refund";
    public const string Release = "release";
    public const string Adjustment = "adjustment";
}

/// <summary>In-process unit wallet for Payments and Sponsorship. Joins the caller's unit of work.</summary>
public interface IUnitWallet
{
    Task<Result<UnitRedemption>> RedeemAsync(UnitRedemptionRequest request, CancellationToken cancellationToken);
    Task<Result<UnitCredit>> CreditAsync(UnitCreditRequest request, CancellationToken cancellationToken);
    Task<long> BalanceAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken);
    Task<Guid> WalletIdAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken);
}

[IntegrationEvent("wallet", "units-purchased", 1)]
public sealed record UnitsPurchased(Guid AggregateId, Guid WalletId, string OwnerType, Guid OwnerId, long Units, Money Value, Guid PaymentIntentId) : IIntegrationEvent;

[IntegrationEvent("wallet", "units-redeemed", 1)]
public sealed record UnitsRedeemed(Guid AggregateId, Guid WalletId, string OwnerType, Guid OwnerId, long Units, Money Value, string Purpose, Guid? ReferenceId) : IIntegrationEvent;
