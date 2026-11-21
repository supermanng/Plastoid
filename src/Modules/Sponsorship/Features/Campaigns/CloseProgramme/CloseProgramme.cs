using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Features.Campaigns.CloseProgramme;

/// <summary>Close-out (blueprint §10 week 14): entry 10 refunds unfilled seats; whatever else remains in the programme sub-ledger follows the contract (refund or rollover).</summary>
public sealed record CloseProgrammeCommand(Guid ProgrammeId, string IdempotencyKey) : ICommand<CloseProgrammeResponse>, IIdempotentCommand;

public sealed record CloseProgrammeResponse(Guid ProgrammeId, string Status, int FilledSeats, int UnfilledSeats, Money UnfilledSeatRefund, Money RemainingFunds, string UnusedFundsPolicy);

internal sealed class CloseProgrammeValidator : AbstractValidator<CloseProgrammeCommand>
{
    public CloseProgrammeValidator()
    {
        RuleFor(x => x.ProgrammeId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class CloseProgrammeHandler(SponsorshipDbContext db, ProgrammeAccess access, ILedgerService ledger, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<CloseProgrammeCommand, CloseProgrammeResponse>
{
    public async Task<Result<CloseProgrammeResponse>> Handle(CloseProgrammeCommand request, CancellationToken cancellationToken)
    {
        var loaded = await access.ForStaffAsync(request.ProgrammeId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<CloseProgrammeResponse>(loaded.Error);
        }

        var programme = loaded.Value;
        var seats = await db.Seats.Where(s => s.ProgrammeId == programme.Id).ToListAsync(cancellationToken);
        var unfilled = seats.Where(s => s.Status is SeatStatus.Open or SeatStatus.Reserved).ToList();
        var filled = seats.Count(s => s.Status == SeatStatus.Filled);
        var now = clock.UtcNow;
        var closed = programme.Close(currentUser.UserId ?? Guid.Empty, unfilled.Count, now);
        if (closed.IsFailure)
        {
            return Result.Failure<CloseProgrammeResponse>(closed.Error);
        }

        foreach (var seat in unfilled)
        {
            if (seat.Status == SeatStatus.Reserved)
            {
                seat.Release("programme_closed", now);
            }

            seat.Refund();
        }

        // Balance before the close-out entry (the entry is flushed with the transaction), less the refund being posted now.
        var remaining = await ledger.GetBalanceAsync(AccountCodes.ProgrammeFundsHeld, programme.Id, cancellationToken) - closed.Value;
        if (!closed.Value.IsZero)
        {
            var posted = await ledger.PostAsync(SponsorshipPostings.Entry10Closeout(programme, closed.Value, unfilled.Count, now), cancellationToken);
            if (posted.IsFailure)
            {
                return Result.Failure<CloseProgrammeResponse>(posted.Error);
            }
        }

        outbox.Enqueue(NotificationRequested.To(programme.CreatedBy, "sponsorship.programme.closed", new Dictionary<string, string>
        {
            ["programmeId"] = programme.Id.ToString("D"),
            ["refund"] = closed.Value.ToString(),
            ["remaining"] = remaining.ToString(),
            ["policy"] = programme.UnusedFundsPolicy,
        }));
        return new CloseProgrammeResponse(programme.Id, programme.Status.ToString().ToLowerInvariant(), filled, unfilled.Count, closed.Value, remaining, programme.UnusedFundsPolicy);
    }
}

internal static class CloseProgrammeEndpoint
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapPost("/{programmeId:guid}/close", async (Guid programmeId, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new CloseProgrammeCommand(programmeId, key), ct)).ToHttpResult();
            })
            .WithName("CloseProgramme")
            .WithSummary("Staff: close the programme; unfilled seats are refunded (entry 10) and the remaining balance reported against the unused-funds policy. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
