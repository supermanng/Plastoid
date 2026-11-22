using Plastoids.Modules.Wallet.Contracts;
using Plastoids.Modules.Wallet.Domain;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Wallet.Services;

public sealed record UnitRateView(long RateKobo, Money RateValue, int Version, DateTime? EffectiveFrom, string Source);

public sealed record UnitLotView(Guid LotId, long UnitsTotal, long UnitsRemaining, Money ValueRemaining, string Kind, DateTime AcquiredAt);

public sealed record WalletTransactionView(Guid TransactionId, string Kind, long Units, Money Value, long BalanceAfter, string Purpose, Guid? ReferenceId, string Description, DateTime OccurredAt);

public sealed record WalletView(Guid WalletId, string OwnerType, Guid OwnerId, long Balance, Money BalanceValue, long RateKobo, long LifetimePurchased, long LifetimeRedeemed, IReadOnlyList<UnitLotView> OpenLots, IReadOnlyList<WalletTransactionView> RecentTransactions, IReadOnlyList<PendingPurchaseView> PendingPurchases);

public sealed record PendingPurchaseView(Guid PurchaseId, Guid PaymentIntentId, long Units, Money Amount, string Status, DateTime CreatedAt);


/// <summary>Read-model projections shared by the wallet and staff slices.</summary>
internal static class WalletViews
{
    public static WalletTransactionView Transaction(WalletTransaction t) => new(t.Id, t.Kind, t.Units, t.Value, t.BalanceAfter, t.Purpose, t.ReferenceId, t.Description, t.OccurredAt);

    public static WalletView Empty(string ownerType, Guid ownerId, UnitRateSnapshot rate) => new(Guid.Empty, ownerType, ownerId, 0, Money.Zero(Money.Naira), rate.RateKobo, 0, 0, [], [], []);

    public static WalletView Wallet(Domain.Wallet wallet, UnitRateSnapshot rate, IEnumerable<UnitLot> openLots, IEnumerable<WalletTransaction> recent, IEnumerable<UnitPurchase> pending)
        => new(wallet.Id, wallet.OwnerType, wallet.OwnerId, wallet.Balance, rate.ValueOf(wallet.Balance), rate.RateKobo, wallet.LifetimePurchased, wallet.LifetimeRedeemed,
            openLots.Select(l => new UnitLotView(l.Id, l.UnitsTotal, l.UnitsRemaining, l.ValueRemaining, l.Kind, l.AcquiredAt)).ToList(),
            recent.Select(Transaction).ToList(),
            pending.Select(p => new PendingPurchaseView(p.Id, p.PaymentIntentId, p.Units, p.Amount, p.Status.ToString().ToLowerInvariant(), p.CreatedAt)).ToList());
}
