using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.Modules.Wallet.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Sponsorship.Features.Seats.GetSeatBlock;

/// <summary>ADR 0021: one seat block with its allocations and the organisation's unit balance.</summary>
public sealed record GetSeatBlockQuery(Guid OrganisationId, Guid BlockId) : IQuery<SeatBlockDetail>;

internal sealed class GetSeatBlockHandler(SponsorshipDbContext db, IUnitWallet wallets) : IQueryHandler<GetSeatBlockQuery, SeatBlockDetail>
{
    public async Task<Result<SeatBlockDetail>> Handle(GetSeatBlockQuery request, CancellationToken cancellationToken)
    {
        var block = await db.SeatBlocks.AsNoTracking().SingleOrDefaultAsync(b => b.Id == request.BlockId && b.OrganisationId == request.OrganisationId, cancellationToken);
        if (block is null)
        {
            return Error.NotFound("block_not_found", "No such seat block for this organisation.");
        }

        var allocations = await db.SeatAllocations.AsNoTracking().Where(a => a.BlockId == block.Id).ToListAsync(cancellationToken);
        var balance = await wallets.BalanceAsync(WalletOwnerTypes.Organisation, block.OrganisationId, cancellationToken);
        return SeatViews.Detail(block, allocations, balance);
    }
}

internal static class GetSeatBlockEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/{blockId:guid}", async (Guid organisationId, Guid blockId, ISender sender, CancellationToken ct) => (await sender.Send(new GetSeatBlockQuery(organisationId, blockId), ct)).ToHttpResult())
            .WithName("GetSeatBlock")
            .WithSummary("Sponsor: a seat block with its allocations and the wallet balance.")
            .RequireAuthorization(IdentityPolicies.OrganisationMember);
    }
}
