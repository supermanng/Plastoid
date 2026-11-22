using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Wallet.Contracts;
using Plastoids.Modules.Wallet.Persistence;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Wallet.Consumers;

/// <summary>payments.payment-succeeded.v1 with purpose "units": credit the lot fixed when checkout started (idempotent on the purchase).</summary>
internal sealed class PaymentSucceededConsumer(WalletDbContext db, IUnitWallet wallets, IOutbox outbox, IClock clock) : IIntegrationEventHandler<PaymentSucceeded>
{
    public async Task HandleAsync(PaymentSucceeded @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (@event.Purpose != PaymentPurposes.Units)
        {
            return;
        }

        var purchase = await db.Purchases.SingleOrDefaultAsync(p => p.PaymentIntentId == @event.PaymentIntentId, cancellationToken);
        if (purchase is null || !purchase.MarkCredited(clock.UtcNow))
        {
            return;
        }

        var wallet = await db.Wallets.AsNoTracking().SingleAsync(w => w.Id == purchase.WalletId, cancellationToken);
        var credit = await wallets.CreditAsync(new UnitCreditRequest(wallet.OwnerType, wallet.OwnerId, purchase.Units, purchase.Amount, $"purchase:{purchase.Id:N}", WalletTransactionKinds.Purchase, "purchase", purchase.PaymentIntentId, $"Bought {purchase.Units} units"), cancellationToken);
        if (credit.IsFailure)
        {
            throw new InvalidOperationException($"Unit purchase {purchase.Id} could not be credited: {credit.Error.Message}");
        }

        outbox.Enqueue(new UnitsPurchased(wallet.Id, wallet.Id, wallet.OwnerType, wallet.OwnerId, purchase.Units, purchase.Amount, purchase.PaymentIntentId));
    }
}

/// <summary>payments.payment-failed.v1: a unit checkout that failed or expired is closed so it stops showing as pending.</summary>
internal sealed class PaymentFailedConsumer(WalletDbContext db, IClock clock) : IIntegrationEventHandler<PaymentFailed>
{
    public async Task HandleAsync(PaymentFailed @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var purchase = await db.Purchases.SingleOrDefaultAsync(p => p.PaymentIntentId == @event.PaymentIntentId, cancellationToken);
        purchase?.MarkFailed(clock.UtcNow);
    }
}
