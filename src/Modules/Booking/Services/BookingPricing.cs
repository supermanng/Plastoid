using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Booking.Persistence;
using Plastoids.Modules.Tutors.Contracts;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Booking.Services;

public sealed record SlotQuote(Guid TutorId, Guid TutorUserId, string Format, DateTime SlotStart, DateTime SlotEnd, int DurationMinutes, Money Price, string TimeZone);

/// <summary>Price and availability of one slot: price rule for (format, duration) else hourly rate pro rata; slot must be inside the tutor's availability and free.</summary>
internal sealed class BookingPricing(ITutorSchedule tutors, BookingDbContext db)
{
    public static readonly int[] AllowedDurations = [30, 45, 60, 90, 120];

    public async Task<Result<SlotQuote>> QuoteAsync(Guid tutorId, string format, DateTime slotStart, int durationMinutes, CancellationToken ct)
    {
        if (!AllowedDurations.Contains(durationMinutes))
        {
            return Error.Validation("duration_invalid", "Duration must be 30, 45, 60, 90 or 120 minutes.");
        }

        var tutor = await tutors.GetAsync(tutorId, ct);
        if (tutor is null || !tutor.IsVerified)
        {
            return Error.NotFound("tutor_not_found", "No verified tutor with that id.");
        }

        if (!tutor.Formats.Contains(format))
        {
            return Error.Validation("format_not_offered", $"This tutor does not offer {format} sessions.");
        }

        var utcStart = DateTime.SpecifyKind(slotStart, DateTimeKind.Utc);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tutor.TimeZone);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcStart, tz)).AddDays(-1);
        var windows = await tutors.SlotsAsync(tutorId, localDate, 3, 15, ct);
        var utcEnd = utcStart.AddMinutes(durationMinutes);
        var covered = windows.Any(w => w.StartUtc <= utcStart && w.EndUtc >= utcEnd) || Covers(windows, utcStart, utcEnd);
        if (!covered)
        {
            return Error.Validation("slot_unavailable", "The tutor is not available at that time.");
        }

        var key = Domain.Booking.KeyFor(tutorId, utcStart);
        if (await db.Bookings.AnyAsync(b => b.SlotKey == key, ct) || await db.Bookings.AnyAsync(b => b.TutorId == tutorId && b.SlotKey != null && b.SlotStart < utcEnd && b.SlotEnd > utcStart, ct))
        {
            return Error.Conflict("slot_taken", "That slot is already booked.");
        }

        var rule = tutor.PriceRules.FirstOrDefault(r => r.Format == format && r.DurationMinutes == durationMinutes);
        var price = rule?.Price ?? new Money((long)Math.Round(tutor.HourlyRate.AmountKobo * durationMinutes / 60m, 0, MidpointRounding.ToEven), tutor.HourlyRate.Currency);
        return new SlotQuote(tutor.TutorId, tutor.UserId, format, utcStart, utcEnd, durationMinutes, price, tutor.TimeZone);
    }

    /// <summary>Contiguous 15-minute windows cover the requested range.</summary>
    private static bool Covers(IReadOnlyList<SlotWindow> windows, DateTime start, DateTime end)
    {
        var cursor = start;
        while (cursor < end)
        {
            var next = windows.FirstOrDefault(w => w.StartUtc <= cursor && w.EndUtc > cursor);
            if (next is null)
            {
                return false;
            }

            cursor = next.EndUtc;
        }

        return true;
    }
}
