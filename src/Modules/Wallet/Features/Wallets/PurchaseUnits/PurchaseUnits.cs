using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Wallet.Contracts;
using Plastoids.Modules.Wallet.Domain;
using Plastoids.Modules.Wallet.Persistence;
using Plastoids.Modules.Wallet.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Wallet.Features.Wallets.PurchaseUnits;

/// <summary>
/// Buy units: the price is fixed at today's rate, a hosted checkout is started through Payments (purpose "units"), and the
/// units land in the wallet when the gateway confirms the charge (payments.payment-succeeded → WalletConsumers).
/// </summary>
public sealed record PurchaseUnitsCommand(long Units, Guid? OrganisationId, string IdempotencyKey) : ICommand<PurchaseUnitsResponse>, IIdempotentCommand;

public sealed record PurchaseUnitsRequest(long Units, Guid? OrganisationId);

public sealed record PurchaseUnitsResponse(Guid PurchaseId, Guid WalletId, Guid PaymentIntentId, long Units, long RateKobo, Money Amount, Uri? CheckoutUrl, string Status);

internal sealed class PurchaseUnitsValidator : AbstractValidator<PurchaseUnitsCommand>
{
    public PurchaseUnitsValidator(IOptions<WalletOptions> options)
    {
        RuleFor(x => x.Units).InclusiveBetween(1, options.Value.MaxUnitsPerPurchase);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class PurchaseUnitsHandler(WalletDbContext db, WalletAccess access, IUnitRates rates, IUnitWallet wallets, IPaymentOperations payments, ICurrentUser currentUser, ICurrentTenant tenant) : ICommandHandler<PurchaseUnitsCommand, PurchaseUnitsResponse>
{
    public async Task<Result<PurchaseUnitsResponse>> Handle(PurchaseUnitsCommand request, CancellationToken cancellationToken)
    {
        var owner = await access.ResolveAsync(request.OrganisationId, spend: true, cancellationToken);
        if (owner.IsFailure)
        {
            return Result.Failure<PurchaseUnitsResponse>(owner.Error);
        }

        var (ownerType, ownerId) = owner.Value;
        var rate = await rates.CurrentAsync(cancellationToken);
        var amount = rate.ValueOf(request.Units);
        var walletId = await wallets.WalletIdAsync(ownerType, ownerId, cancellationToken);
        var payerId = currentUser.UserId!.Value;

        var intent = await payments.CreateIntentAsync(new IntentRequest(payerId, PaymentPurposes.Units, walletId, amount, null, $"{request.Units} units at {new Money(rate.RateKobo, Money.Naira)} each"), cancellationToken);
        if (intent.IsFailure)
        {
            return Result.Failure<PurchaseUnitsResponse>(intent.Error);
        }

        var purchase = UnitPurchase.Start(tenant.TenantId, walletId, payerId, request.Units, rate.RateKobo, intent.Value.PaymentIntentId);
        db.Purchases.Add(purchase);
        return new PurchaseUnitsResponse(purchase.Id, walletId, intent.Value.PaymentIntentId, request.Units, rate.RateKobo, amount, intent.Value.CheckoutUrl, purchase.Status.ToString().ToLowerInvariant());
    }
}

internal static class PurchaseUnitsEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/purchases", async (PurchaseUnitsRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new PurchaseUnitsCommand(body.Units, body.OrganisationId, key), ct)).ToCreatedResult(r => $"/api/v1/payments/intents/{r.PaymentIntentId}");
            })
            .WithName("PurchaseUnits")
            .WithSummary("Buy units at today's rate (for me, or for my organisation as owner/finance): returns the hosted checkout. Units are credited when the gateway confirms. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
