using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Wallet.Contracts;
using Plastoids.Modules.Wallet.Domain;
using Plastoids.Modules.Wallet.Persistence;
using Plastoids.Modules.Wallet.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Wallet.Features.Wallets.GetWallet;

/// <summary>My wallet (or my organisation's): balance, naira value at today's rate, open lots and the latest movements.</summary>
public sealed record GetWalletQuery(Guid? OrganisationId) : IQuery<WalletView>;

public sealed record ListWalletTransactionsQuery(Guid? OrganisationId, string? Cursor, int? Limit) : IQuery<Page<WalletTransactionView>>;

/// <summary>Public: the naira value of one unit right now, so prices can be shown in units everywhere.</summary>
public sealed record GetUnitRateQuery : IQuery<UnitRateView>;

internal sealed class GetWalletHandler(WalletDbContext db, WalletAccess access, IUnitRates rates) : IQueryHandler<GetWalletQuery, WalletView>
{
    public async Task<Result<WalletView>> Handle(GetWalletQuery request, CancellationToken cancellationToken)
    {
        var owner = await access.ResolveAsync(request.OrganisationId, spend: false, cancellationToken);
        if (owner.IsFailure)
        {
            return Result.Failure<WalletView>(owner.Error);
        }

        var (ownerType, ownerId) = owner.Value;
        var rate = await rates.CurrentAsync(cancellationToken);
        var wallet = await db.Wallets.AsNoTracking().SingleOrDefaultAsync(w => w.OwnerType == ownerType && w.OwnerId == ownerId, cancellationToken);
        if (wallet is null)
        {
            return WalletViews.Empty(ownerType, ownerId, rate);
        }

        var lots = await db.Lots.AsNoTracking().Where(l => l.WalletId == wallet.Id && l.UnitsRemaining > 0).OrderBy(l => l.AcquiredAt).ToListAsync(cancellationToken);
        var recent = await db.Transactions.AsNoTracking().Where(t => t.WalletId == wallet.Id).OrderByDescending(t => t.OccurredAt).ThenByDescending(t => t.Id).Take(10).ToListAsync(cancellationToken);
        var pending = await db.Purchases.AsNoTracking().Where(p => p.WalletId == wallet.Id && p.Status == UnitPurchaseStatus.Pending).OrderByDescending(p => p.CreatedAt).Take(5).ToListAsync(cancellationToken);
        return WalletViews.Wallet(wallet, rate, lots, recent, pending);
    }

    }

internal sealed class ListWalletTransactionsHandler(WalletDbContext db, WalletAccess access) : IQueryHandler<ListWalletTransactionsQuery, Page<WalletTransactionView>>
{
    public async Task<Result<Page<WalletTransactionView>>> Handle(ListWalletTransactionsQuery request, CancellationToken cancellationToken)
    {
        var owner = await access.ResolveAsync(request.OrganisationId, spend: false, cancellationToken);
        if (owner.IsFailure)
        {
            return Result.Failure<Page<WalletTransactionView>>(owner.Error);
        }

        var (ownerType, ownerId) = owner.Value;
        var walletId = await db.Wallets.AsNoTracking().Where(w => w.OwnerType == ownerType && w.OwnerId == ownerId).Select(w => (Guid?)w.Id).SingleOrDefaultAsync(cancellationToken);
        if (walletId is null)
        {
            return new Page<WalletTransactionView>([], null);
        }

        var limit = Math.Clamp(request.Limit ?? 50, 1, 200);
        var query = db.Transactions.AsNoTracking().Where(t => t.WalletId == walletId);
        if (Cursor.Decode(request.Cursor) is { } last)
        {
            query = query.Where(t => t.Id.CompareTo(last) < 0);
        }

        var rows = await query.OrderByDescending(t => t.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var next = rows.Count > limit ? Cursor.Encode(rows[limit - 1].Id) : null;
        return new Page<WalletTransactionView>(rows.Take(limit).Select(WalletViews.Transaction).ToList(), next);
    }
}

internal sealed class GetUnitRateHandler(IUnitRates rates) : IQueryHandler<GetUnitRateQuery, UnitRateView>
{
    public async Task<Result<UnitRateView>> Handle(GetUnitRateQuery request, CancellationToken cancellationToken)
    {
        var rate = await rates.CurrentAsync(cancellationToken);
        return new UnitRateView(rate.RateKobo, new Money(rate.RateKobo, Money.Naira), rate.Version, rate.EffectiveFrom == DateTime.MinValue ? null : rate.EffectiveFrom, rate.Source);
    }
}

internal static class GetWalletEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/rate", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetUnitRateQuery(), ct)).ToHttpResult())
            .WithName("GetUnitRate")
            .WithSummary("Public: the naira value of one unit now (admin-approved, four-eyes).")
            .AllowAnonymous();

        group.MapGet("/me", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetWalletQuery(null), ct)).ToHttpResult())
            .WithName("GetMyWallet")
            .WithSummary("My unit wallet: balance, value at today's rate, open lots, recent movements and pending purchases.")
            .RequireAuthorization(Policies.Authenticated);

        group.MapGet("/me/transactions", async (string? cursor, int? limit, ISender sender, CancellationToken ct) => (await sender.Send(new ListWalletTransactionsQuery(null, cursor, limit), ct)).ToHttpResult())
            .WithName("ListMyWalletTransactions")
            .RequireAuthorization(Policies.Authenticated);

        group.MapGet("/organisations/{organisationId:guid}", async (Guid organisationId, ISender sender, CancellationToken ct) => (await sender.Send(new GetWalletQuery(organisationId), ct)).ToHttpResult())
            .WithName("GetOrganisationWallet")
            .WithSummary("Organisation wallet (any member): balance, lots, movements.")
            .RequireAuthorization(Policies.Authenticated);

        group.MapGet("/organisations/{organisationId:guid}/transactions", async (Guid organisationId, string? cursor, int? limit, ISender sender, CancellationToken ct) => (await sender.Send(new ListWalletTransactionsQuery(organisationId, cursor, limit), ct)).ToHttpResult())
            .WithName("ListOrganisationWalletTransactions")
            .RequireAuthorization(Policies.Authenticated);
    }
}
