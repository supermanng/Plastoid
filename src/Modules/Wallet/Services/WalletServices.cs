using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Wallet.Contracts;
using Plastoids.Modules.Wallet.Domain;
using Plastoids.Modules.Wallet.Persistence;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Wallet.Services;

public sealed class WalletOptions
{
    public const string SectionName = "Wallet";

    /// <summary>Naira value of one unit (in kobo) before staff approve a rate. ₦1,000 by default.</summary>
    public long DefaultRateKobo { get; set; } = 100_000;

    /// <summary>Largest single purchase, in units.</summary>
    public long MaxUnitsPerPurchase { get; set; } = 1_000_000;
}

/// <summary>The approved rate in force now, or the configured default when none has been approved yet.</summary>
internal sealed class UnitRates(WalletDbContext db, IUnitOfWork unitOfWork, IOptions<WalletOptions> options, IClock clock) : IUnitRates
{
    public async Task<UnitRateSnapshot> CurrentAsync(CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        var now = clock.UtcNow;
        var approved = await db.Rates.AsNoTracking().Where(r => r.Status == UnitRateStatus.Approved && r.EffectiveFrom <= now).ToListAsync(cancellationToken);
        var rate = UnitRate.EffectiveAt(approved, now);
        return rate is null
            ? new UnitRateSnapshot(options.Value.DefaultRateKobo, 0, DateTime.MinValue, "default")
            : new UnitRateSnapshot(rate.RateKobo, rate.Version, rate.EffectiveFrom, $"v{rate.Version}");
    }
}

/// <summary>Unit bookkeeping: lots consumed first-in, first-out; every change is one append-only transaction row. Money and ledger postings stay in Payments.</summary>
internal sealed class UnitWallet(WalletDbContext db, IUnitOfWork unitOfWork, IOutbox outbox, ICurrentTenant tenant, ICurrentUser currentUser, IClock clock) : IUnitWallet
{
    public async Task<Guid> WalletIdAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        return (await GetOrOpenAsync(ownerType, ownerId, cancellationToken)).Id;
    }

    public async Task<long> BalanceAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        return await db.Wallets.AsNoTracking().Where(w => w.OwnerType == ownerType && w.OwnerId == ownerId).Select(w => w.Balance).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Result<UnitCredit>> CreditAsync(UnitCreditRequest request, CancellationToken cancellationToken)
    {
        if (request.Units <= 0)
        {
            return Error.Validation("units_invalid", "Units must be positive.");
        }

        if (request.Value.IsNegative || request.Value.Currency != Money.Naira)
        {
            return Error.Validation("value_invalid", "Value must be a non-negative naira amount.");
        }

        await EnlistAsync(cancellationToken);
        var existing = await db.Transactions.AsNoTracking().Where(t => t.Reference == request.Reference).Select(t => new { t.WalletId, t.Id, t.Units, t.BalanceAfter }).SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return new UnitCredit(existing.WalletId, existing.Id, existing.Units, existing.BalanceAfter); // idempotent on reference
        }

        var wallet = await GetOrOpenAsync(request.OwnerType, request.OwnerId, cancellationToken);
        var now = clock.UtcNow;
        wallet.ApplyCredit(request.Units, request.Kind);
        db.Lots.Add(UnitLot.Create(tenant.TenantId, wallet.Id, request.Units, request.Value, request.Kind, request.Reference, now));
        var tx = WalletTransaction.Record(tenant.TenantId, wallet.Id, request.Kind, request.Units, request.Value, wallet.Balance, request.Reference, request.Purpose, request.ReferenceId, request.Description, currentUser.UserId, now);
        db.Transactions.Add(tx);
        return new UnitCredit(wallet.Id, tx.Id, request.Units, wallet.Balance);
    }

    public async Task<Result<UnitRedemption>> RedeemAsync(UnitRedemptionRequest request, CancellationToken cancellationToken)
    {
        if (request.Units <= 0)
        {
            return Error.Validation("units_invalid", "Units must be positive.");
        }

        await EnlistAsync(cancellationToken);
        var existing = await db.Transactions.AsNoTracking().Where(t => t.Reference == request.Reference).Select(t => new { t.WalletId, t.Id, t.Units, t.Value, t.BalanceAfter }).SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return new UnitRedemption(existing.WalletId, existing.Id, -existing.Units, existing.Value, existing.BalanceAfter); // idempotent on reference
        }

        var wallet = await GetOrOpenAsync(request.OwnerType, request.OwnerId, cancellationToken);
        if (wallet.Balance < request.Units)
        {
            return Error.Conflict("insufficient_units", $"This needs {request.Units} units; the wallet holds {wallet.Balance}. Buy {request.Units - wallet.Balance} more units first.");
        }

        var lots = await db.Lots.Where(l => l.WalletId == wallet.Id && l.UnitsRemaining > 0).OrderBy(l => l.AcquiredAt).ThenBy(l => l.Id).ToListAsync(cancellationToken);
        var remaining = request.Units;
        var value = Money.Zero(Money.Naira);
        foreach (var lot in lots)
        {
            if (remaining == 0)
            {
                break;
            }

            var (taken, taken_value) = lot.Consume(remaining);
            remaining -= taken;
            value += taken_value;
        }

        if (remaining > 0)
        {
            return Error.Conflict("wallet_lots_inconsistent", "The wallet balance and its lots disagree; contact support.");
        }

        var now = clock.UtcNow;
        wallet.ApplyDebit(request.Units);
        var tx = WalletTransaction.Record(tenant.TenantId, wallet.Id, WalletTransactionKinds.Redemption, -request.Units, value, wallet.Balance, request.Reference, request.Purpose, request.ReferenceId, request.Description, currentUser.UserId, now);
        db.Transactions.Add(tx);
        outbox.Enqueue(new UnitsRedeemed(wallet.Id, wallet.Id, wallet.OwnerType, wallet.OwnerId, request.Units, value, request.Purpose, request.ReferenceId));
        return new UnitRedemption(wallet.Id, tx.Id, request.Units, value, wallet.Balance);
    }

    private async Task<Domain.Wallet> GetOrOpenAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var wallet = await db.Wallets.SingleOrDefaultAsync(w => w.OwnerType == ownerType && w.OwnerId == ownerId, cancellationToken);
        if (wallet is not null)
        {
            return wallet;
        }

        var opened = Domain.Wallet.Open(tenant.TenantId, ownerType, ownerId);
        if (opened.IsFailure)
        {
            throw new InvalidOperationException(opened.Error.Message);
        }

        db.Wallets.Add(opened.Value);
        return opened.Value;
    }

    private async Task EnlistAsync(CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }
    }
}
