using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Wallet.Contracts;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payments.Services;

/// <summary>
/// Pay an intent with units (ADR 0018): whole units at today's rate cover the price (rounded up), the wallet releases them
/// first-in, first-out, and the sale is confirmed at once with entry 12. Shared by the checkout endpoint and the in-process
/// contract used by Booking and Applications.
/// </summary>
internal sealed class UnitCheckout(IUnitRates rates, IUnitWallet wallets, PaymentProcessing processing, IClock clock)
{
    public async Task<Result<long>> PayAsync(PaymentIntent intent, CancellationToken ct)
    {
        if (intent.Purpose == PaymentPurposes.Units)
        {
            return Error.Validation("units_cannot_buy_units", "Units cannot be bought with units.");
        }

        var rate = await rates.CurrentAsync(ct);
        var units = rate.UnitsFor(intent.Amount);
        if (units <= 0)
        {
            return Error.Validation("amount_invalid", "Nothing to pay.");
        }

        var walletId = await wallets.WalletIdAsync(WalletOwnerTypes.User, intent.PayerId, ct);
        var redeemed = await wallets.RedeemAsync(new UnitRedemptionRequest(WalletOwnerTypes.User, intent.PayerId, units, $"payment:{intent.Id:N}", intent.Purpose, intent.ReferenceId, intent.Description), ct);
        if (redeemed.IsFailure)
        {
            return Result.Failure<long>(redeemed.Error);
        }

        intent.AttachUnits(walletId, units, rate.RateKobo, redeemed.Value.Value.AmountKobo);
        var confirmed = await processing.ConfirmFromUnitsAsync(intent, clock.UtcNow, ct);
        return confirmed.IsFailure ? Result.Failure<long>(confirmed.Error) : units;
    }
}
