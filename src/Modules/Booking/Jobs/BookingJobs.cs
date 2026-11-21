using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Booking.Contracts;
using Plastoids.Modules.Booking.Domain;
using Plastoids.Modules.Booking.Persistence;
using Plastoids.Modules.Booking.Services;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;
using Quartz;

namespace Plastoids.Modules.Booking.Jobs;

/// <summary>Every 5 minutes: finished sessions become Completed with a 72-hour earning hold; due holds are released (booking.earning-released.v1).</summary>
[DisallowConcurrentExecution]
public sealed class BookingLifecycleJob(BookingDbContext db, IUnitOfWork unitOfWork, IOutbox outbox, IClock clock, ILogger<BookingLifecycleJob> logger) : IJob
{
    public const string JobName = "booking-lifecycle";

    public async Task Execute(IJobExecutionContext context) => await RunAsync(clock.UtcNow, context.CancellationToken);

    public async Task<(int Completed, int Released)> RunAsync(DateTime now, CancellationToken ct)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await unitOfWork.BeginAsync(db, ct);
        var finished = await db.Bookings.Where(b => b.Status == BookingStatus.Confirmed && b.SlotEnd <= now).Take(200).ToListAsync(ct);
        var completed = 0;
        foreach (var booking in finished)
        {
            if (booking.Complete(now))
            {
                completed++;
                if (!await db.Holds.AnyAsync(h => h.BookingId == booking.Id, ct))
                {
                    // Tutor's share after commission is unknown here; Payments stored it on the intent. The hold carries the gross and Payouts applies the stored rate.
                    db.Holds.Add(EarningHold.Create(booking.TenantId, booking.Id, booking.TutorUserId, booking.Price, booking.SlotEnd));
                }
            }
        }

        // Flush new holds inside the transaction so a hold created and due in the same run is released now, not next run.
        await db.SaveChangesAsync(ct);
        var due = await db.Holds.Where(h => h.Status == HoldStatus.Held && h.ReleasesAt <= now).Take(200).ToListAsync(ct);
        var released = 0;
        foreach (var hold in due)
        {
            if (hold.Release(now))
            {
                released++;
                outbox.Enqueue(new EarningReleased(hold.Id, hold.Id, hold.BookingId, hold.TutorUserId, hold.Amount));
            }
        }

        await unitOfWork.CommitAsync(ct);
        if (completed + released > 0)
        {
            logger.LogInformation("Booking lifecycle: {Completed} completed, {Released} holds released", completed, released);
        }

        return (completed, released);
    }
}

/// <summary>Every 5 minutes: reminders at T-24h and T-1h for confirmed sessions (notifications.send-requested.v1 to both parties).</summary>
[DisallowConcurrentExecution]
public sealed class ReminderJob(ReminderDispatcher dispatcher, IClock clock) : IJob
{
    public const string JobName = "booking-reminders";

    public Task Execute(IJobExecutionContext context) => dispatcher.RunAsync(clock.UtcNow, context.CancellationToken);
}

public sealed class ReminderDispatcher(BookingDbContext db, IUnitOfWork unitOfWork, IOutbox outbox)
{
    public static readonly TimeSpan DayAhead = TimeSpan.FromHours(24);
    public static readonly TimeSpan HourAhead = TimeSpan.FromHours(1);

    /// <summary>Sends each reminder once, for sessions whose start is within the window [now, now + lead] and not yet reminded.</summary>
    public async Task<int> RunAsync(DateTime now, CancellationToken ct)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await unitOfWork.BeginAsync(db, ct);
        var sent = 0;
        var dayWindow = now.Add(DayAhead);
        var day = await db.Bookings.Where(b => b.Status == BookingStatus.Confirmed && b.Reminder24SentAt == null && b.SlotStart > now && b.SlotStart <= dayWindow).Take(200).ToListAsync(ct);
        foreach (var b in day)
        {
            b.MarkReminder24(now);
            Emit(b, "booking.reminder.24h");
            sent++;
        }

        var hourWindow = now.Add(HourAhead);
        var hour = await db.Bookings.Where(b => b.Status == BookingStatus.Confirmed && b.Reminder1SentAt == null && b.SlotStart > now && b.SlotStart <= hourWindow).Take(200).ToListAsync(ct);
        foreach (var b in hour)
        {
            b.MarkReminder1(now);
            Emit(b, "booking.reminder.1h");
            sent++;
        }

        await unitOfWork.CommitAsync(ct);
        return sent;
    }

    private void Emit(Domain.Booking booking, string template)
    {
        var data = BookingLifecycle.Data(booking);
        outbox.Enqueue(NotificationRequested.To(booking.LearnerId, template, data));
        outbox.Enqueue(NotificationRequested.To(booking.TutorUserId, template, data));
    }
}
