using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Applications.Domain;
using Plastoids.Modules.Applications.Persistence;
using Plastoids.Modules.Applications.Services;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Applications.Features.Applying.AcceptOffer;

/// <summary>Accept within 5 days. Partial sponsorship adds a co-pay checkout (Payments.Contracts); an offer on an overlapping programme must be given up first.</summary>
public sealed record AcceptOfferCommand(Guid ApplicationId, Guid? WithdrawFromApplicationId, string IdempotencyKey) : ICommand<AcceptOfferResponse>, IIdempotentCommand;

public sealed record AcceptOfferRequest(Guid? WithdrawFromApplicationId);

public sealed record CoPayCheckout(Guid PaymentIntentId, string GatewayReference, Uri CheckoutUrl, Money Amount, DateTime SeatHeldUntil);

public sealed record AcceptOfferResponse(Guid ApplicationId, string Status, Guid SeatId, CoPayCheckout? CoPay);

public sealed record DeclineOfferCommand(Guid ApplicationId) : ICommand<DeclineOfferResponse>;

public sealed record DeclineOfferResponse(Guid ApplicationId, string Status, int BackfilledOffers);

internal sealed class AcceptOfferValidator : AbstractValidator<AcceptOfferCommand>
{
    public AcceptOfferValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class DeclineOfferValidator : AbstractValidator<DeclineOfferCommand>
{
    public DeclineOfferValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
    }
}

internal sealed class AcceptOfferHandler(ApplicationsDbContext db, ApplicationAccess access, IProgrammeReader programmes, IProgrammeSeats seats, IPaymentOperations payments, OfferService offers, IClock clock) : ICommandHandler<AcceptOfferCommand, AcceptOfferResponse>
{
    public async Task<Result<AcceptOfferResponse>> Handle(AcceptOfferCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MineAsync(request.ApplicationId, cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<AcceptOfferResponse>(mine.Error);
        }

        var application = mine.Value;
        var programme = await programmes.GetAsync(application.ProgrammeId, cancellationToken);
        if (programme is null)
        {
            return Error.NotFound("programme_not_found", "The programme no longer exists.");
        }

        if (application.Status == ApplicationStatus.PendingCoPay)
        {
            return Error.Conflict("co_pay_pending", "The co-pay checkout is already open; complete the payment to confirm the seat.");
        }

        // Overlapping programmes (blueprint §9.13): the learner must choose one.
        var others = await db.Applications.Where(a => a.LearnerId == application.LearnerId && a.Id != application.Id && a.ProgrammeId != application.ProgrammeId
            && (a.Status == ApplicationStatus.Offered || a.Status == ApplicationStatus.PendingCoPay || a.Status == ApplicationStatus.Enrolled)).ToListAsync(cancellationToken);
        foreach (var other in others)
        {
            var otherProgramme = await programmes.GetAsync(other.ProgrammeId, cancellationToken);
            if (otherProgramme is null || otherProgramme.StartsAt >= programme.EndsAt || otherProgramme.EndsAt <= programme.StartsAt)
            {
                continue;
            }

            if (other.Status == ApplicationStatus.Enrolled)
            {
                return Error.Conflict("already_enrolled_overlapping", $"You are already enrolled in '{otherProgramme.Name}', which overlaps this programme.");
            }

            if (request.WithdrawFromApplicationId != other.Id)
            {
                return Error.Conflict("must_choose_programme", $"You also hold an offer for '{otherProgramme.Name}' (application {other.Id}), which overlaps. Pass withdrawFromApplicationId to give it up.");
            }

            var withdrawn = other.Withdraw();
            if (withdrawn.IsFailure)
            {
                return Result.Failure<AcceptOfferResponse>(withdrawn.Error);
            }

            if (other.SeatId is { } otherSeat)
            {
                await seats.ReleaseReservationAsync(otherSeat, "chose_overlapping_programme", cancellationToken);
            }
        }

        var now = clock.UtcNow;
        if (programme.CoPayPerSeat.AmountKobo > 0)
        {
            var intent = await payments.CreateIntentAsync(new IntentRequest(application.LearnerId, PaymentPurposes.CoPay, programme.ProgrammeId, programme.CoPayPerSeat, null, $"Co-pay: {programme.Name}"), cancellationToken);
            if (intent.IsFailure)
            {
                return Result.Failure<AcceptOfferResponse>(intent.Error);
            }

            var pending = application.RequireCoPay(intent.Value.PaymentIntentId, intent.Value.Amount, now);
            if (pending.IsFailure)
            {
                return Result.Failure<AcceptOfferResponse>(pending.Error);
            }

            return new AcceptOfferResponse(application.Id, application.Status.ToString().ToLowerInvariant(), application.SeatId!.Value,
                new CoPayCheckout(intent.Value.PaymentIntentId, intent.Value.GatewayReference, intent.Value.CheckoutUrl, intent.Value.Amount, now + Application.CoPayHold));
        }

        var accepted = application.Accept(now);
        if (accepted.IsFailure)
        {
            return Result.Failure<AcceptOfferResponse>(accepted.Error);
        }

        offers.EmitAccepted(application, programme);
        return new AcceptOfferResponse(application.Id, application.Status.ToString().ToLowerInvariant(), application.SeatId!.Value, null);
    }
}

internal sealed class DeclineOfferHandler(ApplicationAccess access, IProgrammeSeats seats, OfferService offers, IClock clock) : ICommandHandler<DeclineOfferCommand, DeclineOfferResponse>
{
    public async Task<Result<DeclineOfferResponse>> Handle(DeclineOfferCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MineAsync(request.ApplicationId, cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<DeclineOfferResponse>(mine.Error);
        }

        var application = mine.Value;
        var declined = application.Decline();
        if (declined.IsFailure)
        {
            return Result.Failure<DeclineOfferResponse>(declined.Error);
        }

        if (application.SeatId is { } seatId)
        {
            await seats.ReleaseReservationAsync(seatId, "offer_declined", cancellationToken);
        }

        var backfilled = await offers.BackfillAsync(application.ProgrammeId, clock.UtcNow, cancellationToken);
        return new DeclineOfferResponse(application.Id, application.Status.ToString().ToLowerInvariant(), backfilled);
    }
}

internal static class AcceptOfferEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{applicationId:guid}/offer/accept", async (Guid applicationId, AcceptOfferRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new AcceptOfferCommand(applicationId, body?.WithdrawFromApplicationId, key), ct)).ToHttpResult();
            })
            .WithName("AcceptOffer")
            .WithSummary("Learner: accept the offer (5-day expiry). Partial sponsorship returns a co-pay checkout; the seat is held 7 days. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.Authenticated);

        group.MapPost("/{applicationId:guid}/offer/decline", async (Guid applicationId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeclineOfferCommand(applicationId), ct)).ToHttpResult())
            .WithName("DeclineOffer")
            .WithSummary("Learner: decline the offer; the seat goes to the next on the waitlist at once.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
