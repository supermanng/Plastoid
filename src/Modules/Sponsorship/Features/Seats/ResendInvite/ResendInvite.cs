using FluentValidation;
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
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Features.Seats.ResendInvite;

/// <summary>Sends the seat invite again (SMS or email, whichever the seat was given by) to someone who has not signed up yet (ADR 0021).</summary>
public sealed record ResendInviteCommand(Guid OrganisationId, Guid BlockId, Guid AllocationId, string IdempotencyKey) : ICommand<SeatAllocationView>, IIdempotentCommand;

internal sealed class ResendInviteValidator : AbstractValidator<ResendInviteCommand>
{
    public ResendInviteValidator()
    {
        RuleFor(x => x.BlockId).NotEmpty();
        RuleFor(x => x.AllocationId).NotEmpty();
    }
}

internal sealed class ResendInviteHandler(SponsorshipDbContext db, SeatInviteSender invites, IClock clock) : ICommandHandler<ResendInviteCommand, SeatAllocationView>
{
    public async Task<Result<SeatAllocationView>> Handle(ResendInviteCommand request, CancellationToken cancellationToken)
    {
        var block = await db.SeatBlocks.AsNoTracking().SingleOrDefaultAsync(b => b.Id == request.BlockId && b.OrganisationId == request.OrganisationId, cancellationToken);
        if (block is null)
        {
            return Error.NotFound("block_not_found", "No such seat block for this organisation.");
        }

        if (block.Status != SeatBlockStatus.Active)
        {
            return Error.Conflict("block_closed", "The seat block is closed.");
        }

        var allocation = await db.SeatAllocations.SingleOrDefaultAsync(a => a.Id == request.AllocationId && a.BlockId == block.Id, cancellationToken);
        if (allocation is null)
        {
            return Error.NotFound("allocation_not_found", "No such seat in this block.");
        }

        var sent = await invites.SendAsync(block, allocation, clock.UtcNow, cancellationToken);
        return sent.IsFailure ? Result.Failure<SeatAllocationView>(sent.Error) : SeatViews.Allocation(allocation);
    }
}

internal static class ResendInviteEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{blockId:guid}/allocations/{allocationId:guid}/resend-invite", async (Guid organisationId, Guid blockId, Guid allocationId, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ResendInviteCommand(organisationId, blockId, allocationId, key), ct)).ToHttpResult();
            })
            .WithName("ResendSeatInvite")
            .WithSummary("Sponsor owner/finance: send the seat invite again to someone who has not signed up yet. Requires Idempotency-Key.")
            .RequireAuthorization(IdentityPolicies.OrganisationFinance);
    }
}
