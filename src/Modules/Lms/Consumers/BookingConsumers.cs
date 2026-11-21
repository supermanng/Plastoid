using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Modules.Booking.Contracts;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Lms.Consumers;

/// <summary>booking.booking-confirmed.v1 → a live session with a provider room (idempotent on booking id).</summary>
internal sealed class BookingConfirmedConsumer(LmsDbContext db, ILiveVideoProvider video) : IIntegrationEventHandler<BookingConfirmed>
{
    public async Task HandleAsync(BookingConfirmed @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (await db.LiveSessions.AnyAsync(s => s.BookingId == @event.BookingId, cancellationToken))
        {
            return;
        }

        var sessionId = Plastoids.SharedKernel.Domain.Entity.NewId();
        var room = await video.CreateRoomAsync(sessionId, @event.SlotStart, @event.SlotEnd, cancellationToken);
        if (room.IsFailure)
        {
            throw new InvalidOperationException($"Live room could not be created: {room.Error.Message}");
        }

        db.LiveSessions.Add(LiveSession.Schedule(envelope.TenantId, LiveSessionSources.Booking, @event.BookingId, null, @event.TutorUserId, [@event.LearnerId], $"{@event.Format} session", @event.SlotStart, @event.SlotEnd, video.Name, room.Value.RoomId, room.Value.RoomUrl.ToString()));
    }
}

internal sealed class BookingRescheduledConsumer(LmsDbContext db) : IIntegrationEventHandler<BookingRescheduled>
{
    public async Task HandleAsync(BookingRescheduled @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var session = await db.LiveSessions.SingleOrDefaultAsync(s => s.BookingId == @event.BookingId, cancellationToken);
        session?.Move(@event.SlotStart, @event.SlotEnd);
    }
}

internal sealed class BookingCancelledConsumer(LmsDbContext db) : IIntegrationEventHandler<BookingCancelled>
{
    public async Task HandleAsync(BookingCancelled @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var session = await db.LiveSessions.SingleOrDefaultAsync(s => s.BookingId == @event.BookingId, cancellationToken);
        session?.Cancel();
    }
}
