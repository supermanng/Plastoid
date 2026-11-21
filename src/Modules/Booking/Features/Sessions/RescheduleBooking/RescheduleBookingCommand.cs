using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Booking.Contracts;
using Plastoids.Modules.Booking.Domain;
using Plastoids.Modules.Booking.Persistence;
using Plastoids.Modules.Booking.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Booking.Features.Sessions.RescheduleBooking;

public sealed record RescheduleBookingCommand(Guid BookingId, DateTime NewSlotStart) : ICommand<RescheduleBookingResponse>;

public sealed record RescheduleBookingRequest(DateTime NewSlotStart);

public sealed record RescheduleBookingResponse(Guid BookingId, DateTime SlotStart, DateTime SlotEnd, DateTime? RescheduledFrom);

internal sealed class RescheduleBookingValidator : AbstractValidator<RescheduleBookingCommand>
{
    public RescheduleBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
    }
}

internal sealed class RescheduleBookingHandler(BookingDbContext db, BookingPricing pricing, SlotLocks locks, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<RescheduleBookingCommand, RescheduleBookingResponse>
{
    public async Task<Result<RescheduleBookingResponse>> Handle(RescheduleBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);
        if (booking is null)
        {
            return Error.NotFound("booking_not_found", "No such booking.");
        }

        var userId = currentUser.UserId ?? Guid.Empty;
        var actor = booking.LearnerId == userId ? CancellationActors.Learner : booking.TutorUserId == userId ? CancellationActors.Tutor : null;
        if (actor is null)
        {
            return Error.Forbidden("not_your_booking", "You are not part of this booking.");
        }

        var quote = await pricing.QuoteAsync(booking.TutorId, booking.Format, request.NewSlotStart, booking.DurationMinutes, cancellationToken);
        if (quote.IsFailure)
        {
            return Result.Failure<RescheduleBookingResponse>(quote.Error);
        }

        if (!await locks.TryLockAsync(booking.TutorId, quote.Value.SlotStart, booking.LearnerId))
        {
            return Error.Conflict("slot_locked", "Another learner is checking out that slot.");
        }

        var oldStart = booking.SlotStart;
        var moved = booking.Reschedule(actor, quote.Value.SlotStart, clock.UtcNow);
        if (moved.IsFailure)
        {
            return Result.Failure<RescheduleBookingResponse>(moved.Error);
        }

        await locks.ReleaseAsync(booking.TutorId, oldStart);
        outbox.Enqueue(new BookingRescheduled(booking.Id, booking.Id, booking.SlotStart, booking.SlotEnd));
        var data = BookingLifecycle.Data(booking);
        data["rescheduledFrom"] = oldStart.ToString("O");
        outbox.Enqueue(NotificationRequested.To(booking.LearnerId, "booking.rescheduled.learner", data));
        outbox.Enqueue(NotificationRequested.To(booking.TutorUserId, "booking.rescheduled.tutor", data));
        return new RescheduleBookingResponse(booking.Id, booking.SlotStart, booking.SlotEnd, booking.RescheduledFrom);
    }
}

internal static class RescheduleBookingEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{bookingId:guid}/reschedule", async (Guid bookingId, RescheduleBookingRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RescheduleBookingCommand(bookingId, body.NewSlotStart), ct)).ToHttpResult())
            .WithName("RescheduleBooking")
            .WithSummary("Move a confirmed session (learner ≥24 h notice; tutor any time before it starts).")
            .RequireAuthorization(Policies.Authenticated);
    }
}
