using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Booking.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Booking.Features.Sessions.ListMyBookings;

/// <summary>role = learner (default) | tutor; upcoming = true lists confirmed future sessions only.</summary>
public sealed record ListMyBookingsQuery(string Role, bool Upcoming, string? Cursor, int? Limit) : IQuery<Page<BookingSummary>>;

public sealed record BookingSummary(Guid BookingId, Guid TutorId, Guid TutorUserId, Guid LearnerId, string Format, DateTime SlotStart, DateTime SlotEnd, Money Price, string Status, Guid? PackageId, int RefundPercent);

internal sealed class ListMyBookingsHandler(BookingDbContext db, ICurrentUser currentUser, Plastoids.SharedKernel.Time.IClock clock) : IQueryHandler<ListMyBookingsQuery, Page<BookingSummary>>
{
    public async Task<Result<Page<BookingSummary>>> Handle(ListMyBookingsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var limit = Cursor.ClampLimit(request.Limit);
        var query = request.Role == "tutor" ? db.Bookings.AsNoTracking().Where(b => b.TutorUserId == userId) : db.Bookings.AsNoTracking().Where(b => b.LearnerId == userId);
        if (request.Upcoming)
        {
            var now = clock.UtcNow;
            query = query.Where(b => b.Status == Domain.BookingStatus.Confirmed && b.SlotEnd > now);
        }

        if (Cursor.Decode(request.Cursor) is { } after)
        {
            query = query.Where(b => b.Id.CompareTo(after) < 0);
        }

        var rows = await query.OrderByDescending(b => b.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;
        var items = rows.Take(limit).Select(b => new BookingSummary(b.Id, b.TutorId, b.TutorUserId, b.LearnerId, b.Format, b.SlotStart, b.SlotEnd, b.Price, b.Status.ToString().ToLowerInvariant(), b.PackageId, b.RefundPercent)).ToList();
        return new Page<BookingSummary>(items, hasMore ? Cursor.Encode(items[^1].BookingId) : null);
    }
}

internal static class ListMyBookingsEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/me", async (string? role, bool? upcoming, string? cursor, int? limit, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListMyBookingsQuery(role ?? "learner", upcoming ?? false, cursor, limit), ct)).ToHttpResult())
            .WithName("ListMyBookings")
            .WithSummary("My bookings as learner (default) or tutor (?role=tutor); ?upcoming=true for confirmed future sessions.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
