using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Wallet.Contracts;
using Plastoids.Modules.Wallet.Services;
using Plastoids.Modules.Wallet.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Wallet.Features.Admin.AdjustWallet;

/// <summary>Staff lookup of any wallet by owner, and goodwill / correction adjustments (audited, reason required). Adjustments carry no cash, so their lot value is zero and they never touch the ledger.</summary>
public sealed record LookupWalletQuery(string OwnerType, Guid OwnerId) : IQuery<WalletView>;

public sealed record AdjustWalletCommand(string OwnerType, Guid OwnerId, long Units, string Reason, string IdempotencyKey) : ICommand<AdjustWalletResponse>, IIdempotentCommand;

public sealed record AdjustWalletRequest(long Units, string Reason);

public sealed record AdjustWalletResponse(Guid WalletId, Guid TransactionId, long Units, long BalanceAfter);

internal sealed class AdjustWalletValidator : AbstractValidator<AdjustWalletCommand>
{
    public AdjustWalletValidator()
    {
        RuleFor(x => x.OwnerType).Must(WalletOwnerTypes.IsValid).WithMessage("ownerType must be user or organisation.");
        RuleFor(x => x.OwnerId).NotEmpty();
        RuleFor(x => x.Units).NotEqual(0).InclusiveBetween(-1_000_000, 1_000_000);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class LookupWalletHandler(WalletDbContext db, IUnitRates rates) : IQueryHandler<LookupWalletQuery, WalletView>
{
    public async Task<Result<WalletView>> Handle(LookupWalletQuery request, CancellationToken cancellationToken)
    {
        if (!WalletOwnerTypes.IsValid(request.OwnerType))
        {
            return Error.Validation("wallet_owner_type_invalid", "ownerType must be user or organisation.");
        }

        var rate = await rates.CurrentAsync(cancellationToken);
        var wallet = await db.Wallets.AsNoTracking().SingleOrDefaultAsync(w => w.OwnerType == request.OwnerType && w.OwnerId == request.OwnerId, cancellationToken);
        if (wallet is null)
        {
            return WalletViews.Empty(request.OwnerType, request.OwnerId, rate);
        }

        var lots = await db.Lots.AsNoTracking().Where(l => l.WalletId == wallet.Id && l.UnitsRemaining > 0).OrderBy(l => l.AcquiredAt).ToListAsync(cancellationToken);
        var recent = await db.Transactions.AsNoTracking().Where(t => t.WalletId == wallet.Id).OrderByDescending(t => t.Id).Take(25).ToListAsync(cancellationToken);
        return WalletViews.Wallet(wallet, rate, lots, recent, []);
    }
}

internal sealed class AdjustWalletHandler(IUnitWallet wallets) : ICommandHandler<AdjustWalletCommand, AdjustWalletResponse>
{
    public async Task<Result<AdjustWalletResponse>> Handle(AdjustWalletCommand request, CancellationToken cancellationToken)
    {
        var reference = $"adjustment:{request.IdempotencyKey}";
        if (request.Units > 0)
        {
            var credit = await wallets.CreditAsync(new UnitCreditRequest(request.OwnerType, request.OwnerId, request.Units, Money.Zero(Money.Naira), reference, WalletTransactionKinds.Adjustment, "adjustment", null, request.Reason), cancellationToken);
            return credit.IsFailure ? Result.Failure<AdjustWalletResponse>(credit.Error) : new AdjustWalletResponse(credit.Value.WalletId, credit.Value.TransactionId, request.Units, credit.Value.BalanceAfter);
        }

        var debit = await wallets.RedeemAsync(new UnitRedemptionRequest(request.OwnerType, request.OwnerId, -request.Units, reference, "adjustment", null, request.Reason), cancellationToken);
        return debit.IsFailure ? Result.Failure<AdjustWalletResponse>(debit.Error) : new AdjustWalletResponse(debit.Value.WalletId, debit.Value.TransactionId, request.Units, debit.Value.BalanceAfter);
    }
}

internal static class AdjustWalletEndpoints
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapGet("/{ownerType}/{ownerId:guid}", async (string ownerType, Guid ownerId, ISender sender, CancellationToken ct) => (await sender.Send(new LookupWalletQuery(ownerType, ownerId), ct)).ToHttpResult())
            .WithName("LookupWallet")
            .WithSummary("Staff: any wallet by owner (user | organisation) with lots and the latest movements.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapPost("/{ownerType}/{ownerId:guid}/adjustments", async (string ownerType, Guid ownerId, AdjustWalletRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new AdjustWalletCommand(ownerType, ownerId, body.Units, body.Reason, key), ct)).ToHttpResult();
            })
            .WithName("AdjustWallet")
            .WithSummary("Staff: add (goodwill) or remove units with a reason; audited, no cash moves. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
