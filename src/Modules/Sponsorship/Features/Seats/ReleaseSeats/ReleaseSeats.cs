using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Features.Seats.ReleaseSeats;

/// <summary>ADR 0021 §4: the sponsor gives back open seats; the cohort reservation is freed, units return to the wallet pro rata (kind release) and entry 16 is posted.</summary>
public sealed record ReleaseSeatsCommand(Guid OrganisationId, Guid BlockId, int Seats, string IdempotencyKey) : ICommand<SeatBlockView>, IIdempotentCommand;

public sealed record ReleaseSeatsRequest(int Seats);

internal sealed class ReleaseSeatsValidator : AbstractValidator<ReleaseSeatsCommand>
{
    public ReleaseSeatsValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
        RuleFor(x => x.BlockId).NotEmpty();
        RuleFor(x => x.Seats).GreaterThanOrEqualTo(1).WithErrorCode("seats_invalid");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ReleaseSeatsHandler(SponsorshipDbContext db, SeatReleaser releaser, IClock clock) : ICommandHandler<ReleaseSeatsCommand, SeatBlockView>
{
    public async Task<Result<SeatBlockView>> Handle(ReleaseSeatsCommand request, CancellationToken cancellationToken)
    {
        var block = await db.SeatBlocks.SingleOrDefaultAsync(b => b.Id == request.BlockId && b.OrganisationId == request.OrganisationId, cancellationToken);
        if (block is null)
        {
            return Error.NotFound("block_not_found", "No such seat block for this organisation.");
        }

        var released = await releaser.ReleaseAsync(block, request.Seats, clock.UtcNow, cancellationToken);
        if (released.IsFailure)
        {
            return Result.Failure<SeatBlockView>(released.Error);
        }

        var allocations = await db.SeatAllocations.AsNoTracking().Where(a => a.BlockId == block.Id).ToListAsync(cancellationToken);
        return SeatViews.Block(block, allocations);
    }
}

internal static class ReleaseSeatsEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{blockId:guid}/release", async (Guid organisationId, Guid blockId, ReleaseSeatsRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ReleaseSeatsCommand(organisationId, blockId, body.Seats, key), ct)).ToHttpResult();
            })
            .WithName("ReleaseSeats")
            .WithSummary("Sponsor owner/finance: release open seats back to the wallet (entry 16). Requires Idempotency-Key.")
            .RequireAuthorization(IdentityPolicies.OrganisationFinance);
    }
}
