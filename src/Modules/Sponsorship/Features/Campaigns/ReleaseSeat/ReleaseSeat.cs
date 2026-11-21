using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Features.Campaigns.ReleaseSeat;

/// <summary>A drop-out gives the seat back; sponsorship.seat-released.v1 lets Applications backfill from the waitlist.</summary>
public sealed record ReleaseSeatCommand(Guid ProgrammeId, Guid SeatId, string Reason, string IdempotencyKey) : ICommand<ReleaseSeatResponse>, IIdempotentCommand;

public sealed record ReleaseSeatRequest(string Reason);

public sealed record ReleaseSeatResponse(Guid SeatId, string Status, int OpenSeats);

internal sealed class ReleaseSeatValidator : AbstractValidator<ReleaseSeatCommand>
{
    public ReleaseSeatValidator()
    {
        RuleFor(x => x.ProgrammeId).NotEmpty();
        RuleFor(x => x.SeatId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ReleaseSeatHandler(SponsorshipDbContext db, IOutbox outbox, IClock clock) : ICommandHandler<ReleaseSeatCommand, ReleaseSeatResponse>
{
    public async Task<Result<ReleaseSeatResponse>> Handle(ReleaseSeatCommand request, CancellationToken cancellationToken)
    {
        var seat = await db.Seats.SingleOrDefaultAsync(s => s.Id == request.SeatId && s.ProgrammeId == request.ProgrammeId, cancellationToken);
        if (seat is null)
        {
            return Error.NotFound("seat_not_found", "No such seat on this programme.");
        }

        var released = seat.Release(request.Reason, clock.UtcNow);
        if (released.IsFailure)
        {
            return Result.Failure<ReleaseSeatResponse>(released.Error);
        }

        outbox.Enqueue(new SeatReleased(seat.ProgrammeId, seat.ProgrammeId, seat.Id, request.Reason.Trim()));
        var open = await db.Seats.CountAsync(s => s.ProgrammeId == seat.ProgrammeId && s.Status == Domain.SeatStatus.Open, cancellationToken) + 1;
        return new ReleaseSeatResponse(seat.Id, seat.Status.ToString().ToLowerInvariant(), open);
    }
}

internal static class ReleaseSeatEndpoint
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapPost("/{programmeId:guid}/seats/{seatId:guid}/release", async (Guid programmeId, Guid seatId, ReleaseSeatRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ReleaseSeatCommand(programmeId, seatId, body.Reason, key), ct)).ToHttpResult();
            })
            .WithName("ReleaseSeat")
            .WithSummary("Staff: release a reserved or filled seat (drop-out); the waitlist backfills it. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
