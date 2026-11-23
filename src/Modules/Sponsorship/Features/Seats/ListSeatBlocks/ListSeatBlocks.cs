using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Sponsorship.Features.Seats.ListSeatBlocks;

/// <summary>ADR 0021: the organisation's seat blocks, newest first.</summary>
public sealed record ListSeatBlocksQuery(Guid OrganisationId) : IQuery<IReadOnlyList<SeatBlockView>>;

internal sealed class ListSeatBlocksHandler(SponsorshipDbContext db) : IQueryHandler<ListSeatBlocksQuery, IReadOnlyList<SeatBlockView>>
{
    public async Task<Result<IReadOnlyList<SeatBlockView>>> Handle(ListSeatBlocksQuery request, CancellationToken cancellationToken)
    {
        var blocks = await db.SeatBlocks.AsNoTracking().Where(b => b.OrganisationId == request.OrganisationId).OrderByDescending(b => b.PurchasedAt).ToListAsync(cancellationToken);
        var ids = blocks.Select(b => b.Id).ToList();
        var counts = await db.SeatAllocations.AsNoTracking().Where(a => ids.Contains(a.BlockId))
            .GroupBy(a => a.BlockId)
            .Select(g => new { BlockId = g.Key, Enrolled = g.Count(a => a.Status == SeatAllocationStatus.Enrolled), Invited = g.Count(a => a.Status == SeatAllocationStatus.Invited) })
            .ToDictionaryAsync(x => x.BlockId, cancellationToken);
        return blocks.Select(b => counts.TryGetValue(b.Id, out var c) ? SeatViews.Block(b, c.Enrolled, c.Invited) : SeatViews.Block(b, 0, 0)).ToList();
    }
}

internal static class ListSeatBlocksEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (Guid organisationId, ISender sender, CancellationToken ct) => (await sender.Send(new ListSeatBlocksQuery(organisationId), ct)).ToHttpResult())
            .WithName("ListSeatBlocks")
            .WithSummary("Sponsor: the organisation's seat blocks.")
            .RequireAuthorization(IdentityPolicies.OrganisationMember);
    }
}
