using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Tutors.Contracts;

public sealed record TutorPriceRule(string Format, int DurationMinutes, Money Price, int? MaxLearners);

public sealed record TutorScheduleInfo(Guid TutorId, Guid UserId, bool IsVerified, string TimeZone, Money HourlyRate, IReadOnlyList<string> Formats, IReadOnlyList<TutorPriceRule> PriceRules, bool CalendarConnected);

public sealed record SlotWindow(DateTime StartUtc, DateTime EndUtc);

/// <summary>In-process read contract for Booking: who the tutor is, what they charge and when they are available.</summary>
public interface ITutorSchedule
{
    Task<TutorScheduleInfo?> GetAsync(Guid tutorId, CancellationToken cancellationToken);

    /// <summary>Bookable UTC slots from the tutor's rules and exceptions for the window (does not know about bookings).</summary>
    Task<IReadOnlyList<SlotWindow>> SlotsAsync(Guid tutorId, DateOnly fromLocal, int days, int slotMinutes, CancellationToken cancellationToken);
}
