using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Booking.Domain;
using Plastoids.Modules.Booking.Persistence;
using Plastoids.Modules.Booking.Services;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Booking.Features.Sessions.CancelBooking;

/// <summary>Story D2: cancellation with the §9 refund rules; refundTo = credit (default) | card.</summary>
public sealed record CancelBookingCommand(Guid BookingId, string Reason, bool TutorNoShow, string RefundTo, string IdempotencyKey) : ICommand<CancelBookingResponse>, IIdempotentCommand;

public sealed record CancelBookingRequest(string Reason, bool? TutorNoShow, string? RefundTo);

public sealed record CancelBookingResponse(Guid BookingId, string Status, string CancelledBy, int RefundPercent, bool TutorStrike);

internal sealed class CancelBookingValidator : AbstractValidator<CancelBookingCommand>
{
    public CancelBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(3).MaximumLength(500);
        RuleFor(x => x.RefundTo).Must(r => r is RefundDestinations.Credit or RefundDestinations.Card).WithMessage("refundTo must be credit or card.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class CancelBookingHandler(BookingDbContext db, BookingLifecycle lifecycle, ICurrentUser currentUser) : ICommandHandler<CancelBookingCommand, CancelBookingResponse>
{
    public async Task<Result<CancelBookingResponse>> Handle(CancelBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);
        if (booking is null)
        {
            return Error.NotFound("booking_not_found", "No such booking.");
        }

        var userId = currentUser.UserId ?? Guid.Empty;
        string actor;
        if (booking.LearnerId == userId)
        {
            actor = CancellationActors.Learner;
        }
        else if (booking.TutorUserId == userId)
        {
            actor = CancellationActors.Tutor;
        }
        else if (currentUser.Roles.Contains("staff"))
        {
            actor = CancellationActors.System;
        }
        else
        {
            return Error.Forbidden("not_your_booking", "You are not part of this booking.");
        }

        var outcome = await lifecycle.CancelAsync(booking, actor, request.Reason, request.TutorNoShow, request.RefundTo, cancellationToken);
        if (outcome.IsFailure)
        {
            return Result.Failure<CancelBookingResponse>(outcome.Error);
        }

        return new CancelBookingResponse(booking.Id, booking.Status.ToString().ToLowerInvariant(), actor, outcome.Value.RefundPercent, outcome.Value.TutorStrike);
    }
}

internal static class CancelBookingEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{bookingId:guid}/cancel", async (Guid bookingId, CancelBookingRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new CancelBookingCommand(bookingId, body.Reason, body.TutorNoShow ?? false, body.RefundTo ?? RefundDestinations.Credit, key), ct)).ToHttpResult();
            })
            .WithName("CancelBooking")
            .WithSummary("Learner/tutor cancel (>24 h full, <24 h 50 %, tutor 100 %) or learner reports a no-show (100 % + strike). Requires Idempotency-Key.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
