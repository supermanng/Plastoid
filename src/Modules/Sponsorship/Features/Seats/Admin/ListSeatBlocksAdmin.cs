using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.Modules.Wallet.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Sponsorship.Features.Seats.Admin;

/// <summary>ADR 0021 staff oversight: seat blocks across every organisation, newest first, optionally by status (active | closed).</summary>
public sealed record ListSeatBlocksAdminQuery(string? Status, int? Limit) : IQuery<IReadOnlyList<SeatBlockView>>;

public sealed record GetSeatBlockAdminQuery(Guid BlockId) : IQuery<SeatBlockDetail>;

internal sealed class ListSeatBlocksAdminHandler(SponsorshipDbContext db) : IQueryHandler<ListSeatBlocksAdminQuery, IReadOnlyList<SeatBlockView>>
{
    public async Task<Result<IReadOnlyList<SeatBlockView>>> Handle(ListSeatBlocksAdminQuery request, CancellationToken cancellationToken)
    {
        var query = db.SeatBlocks.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<SeatBlockStatus>(request.Status, ignoreCase: true, out var status))
            {
                return Error.Validation("status_invalid", "status must be active or closed.");
            }

            query = query.Where(b => b.Status == status);
        }

        var blocks = await query.OrderByDescending(b => b.PurchasedAt).Take(Cursor.ClampLimit(request.Limit)).ToListAsync(cancellationToken);
        var ids = blocks.Select(b => b.Id).ToList();
        var counts = await db.SeatAllocations.AsNoTracking().Where(a => ids.Contains(a.BlockId))
            .GroupBy(a => a.BlockId)
            .Select(g => new { BlockId = g.Key, Enrolled = g.Count(a => a.Status == SeatAllocationStatus.Enrolled), Invited = g.Count(a => a.Status == SeatAllocationStatus.Invited) })
            .ToDictionaryAsync(x => x.BlockId, cancellationToken);
        return blocks.Select(b => counts.TryGetValue(b.Id, out var c) ? SeatViews.Block(b, c.Enrolled, c.Invited) : SeatViews.Block(b, 0, 0)).ToList();
    }
}

internal sealed class GetSeatBlockAdminHandler(SponsorshipDbContext db, IUnitWallet wallets) : IQueryHandler<GetSeatBlockAdminQuery, SeatBlockDetail>
{
    public async Task<Result<SeatBlockDetail>> Handle(GetSeatBlockAdminQuery request, CancellationToken cancellationToken)
    {
        var block = await db.SeatBlocks.AsNoTracking().SingleOrDefaultAsync(b => b.Id == request.BlockId, cancellationToken);
        if (block is null)
        {
            return Error.NotFound("block_not_found", "No such seat block.");
        }

        var allocations = await db.SeatAllocations.AsNoTracking().Where(a => a.BlockId == block.Id).ToListAsync(cancellationToken);
        var balance = await wallets.BalanceAsync(WalletOwnerTypes.Organisation, block.OrganisationId, cancellationToken);
        return SeatViews.Detail(block, allocations, balance);
    }
}

internal static class SeatBlocksAdminEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (string? status, int? limit, ISender sender, CancellationToken ct) => (await sender.Send(new ListSeatBlocksAdminQuery(status, limit), ct)).ToHttpResult())
            .WithName("AdminListSeatBlocks")
            .WithSummary("Staff: seat blocks across all organisations.")
            .RequireAuthorization(Policies.Staff);
        group.MapGet("/{blockId:guid}", async (Guid blockId, ISender sender, CancellationToken ct) => (await sender.Send(new GetSeatBlockAdminQuery(blockId), ct)).ToHttpResult())
            .WithName("AdminGetSeatBlock")
            .WithSummary("Staff: a seat block with its allocations.")
            .RequireAuthorization(Policies.Staff);
    }
}
