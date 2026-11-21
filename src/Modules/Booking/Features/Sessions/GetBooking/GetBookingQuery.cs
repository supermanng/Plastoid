using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Booking.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Booking.Features.Sessions.GetBooking;

public sealed record GetBookingQuery(Guid BookingId) : IQuery<BookingDetail>;

public sealed record BookingDetail(Guid BookingId, Guid TutorId, Guid TutorUserId, Guid LearnerId, string Format, DateTime SlotStart, DateTime SlotEnd, int DurationMinutes, Money Price, string Status, Guid? PaymentIntentId, Guid? PackageId, DateTime? ConfirmedAt, DateTime? CancelledAt, string? CancelledBy, string? CancellationReason, int RefundPercent, DateTime? RescheduledFrom, string? Note);

internal sealed class GetBookingHandler(BookingDbContext db, ICurrentUser currentUser) : IQueryHandler<GetBookingQuery, BookingDetail>
{
    public async Task<Result<BookingDetail>> Handle(GetBookingQuery request, CancellationToken cancellationToken)
    {
        var b = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.BookingId, cancellationToken);
        var userId = currentUser.UserId ?? Guid.Empty;
        if (b is null || (b.LearnerId != userId && b.TutorUserId != userId && !currentUser.Roles.Contains("staff")))
        {
            return Error.NotFound("booking_not_found", "No such booking.");
        }

        return new BookingDetail(b.Id, b.TutorId, b.TutorUserId, b.LearnerId, b.Format, b.SlotStart, b.SlotEnd, b.DurationMinutes, b.Price, b.Status.ToString().ToLowerInvariant(), b.PaymentIntentId, b.PackageId, b.ConfirmedAt, b.CancelledAt, b.CancelledBy, b.CancellationReason, b.RefundPercent, b.RescheduledFrom, b.Note);
    }
}

internal static class GetBookingEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/{bookingId:guid}", async (Guid bookingId, ISender sender, CancellationToken ct) => (await sender.Send(new GetBookingQuery(bookingId), ct)).ToHttpResult())
            .WithName("GetBooking")
            .RequireAuthorization(Policies.Authenticated);
    }
}
