using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Tutors.Domain;

public enum AvailabilityKind
{
    Weekly = 0,
    Exception = 1,
}

/// <summary>
/// Weekly rule (DayOfWeek + window) or a dated exception (window added, or whole day/window blocked).
/// Times are local to the tutor's profile time zone; slots are projected to UTC by <see cref="AvailabilityCalendar"/>.
/// </summary>
public sealed class Availability : Entity
{
    private Availability()
    {
    }

    private Availability(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid TutorProfileId { get; private set; }
    public AvailabilityKind Kind { get; private set; }
    public DayOfWeek? Day { get; private set; }
    public DateOnly? Date { get; private set; }
    public TimeOnly Start { get; private set; }
    public TimeOnly End { get; private set; }
    public bool Available { get; private set; }

    public static Availability Weekly(Guid tenantId, Guid tutorProfileId, DayOfWeek day, TimeOnly start, TimeOnly end)
        => new(NewId(), tenantId) { TutorProfileId = tutorProfileId, Kind = AvailabilityKind.Weekly, Day = day, Start = start, End = end, Available = true };

    public static Availability Exception(Guid tenantId, Guid tutorProfileId, DateOnly date, TimeOnly start, TimeOnly end, bool available)
        => new(NewId(), tenantId) { TutorProfileId = tutorProfileId, Kind = AvailabilityKind.Exception, Date = date, Start = start, End = end, Available = available };
}

public sealed record Slot(DateTime StartUtc, DateTime EndUtc);

/// <summary>Projects rules + exceptions into bookable UTC slots (blueprint C3: time zone aware; buffer between slots).</summary>
public static class AvailabilityCalendar
{
    public static IReadOnlyList<Slot> Slots(IEnumerable<Availability> rules, string timeZone, DateOnly fromLocal, int days, int slotMinutes = 60, int bufferMinutes = 0)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var all = rules.ToList();
        var result = new List<Slot>();
        for (var i = 0; i < days; i++)
        {
            var date = fromLocal.AddDays(i);
            var windows = new List<(TimeOnly Start, TimeOnly End)>();
            windows.AddRange(all.Where(r => r.Kind == AvailabilityKind.Weekly && r.Day == date.DayOfWeek).Select(r => (r.Start, r.End)));
            windows.AddRange(all.Where(r => r.Kind == AvailabilityKind.Exception && r.Date == date && r.Available).Select(r => (r.Start, r.End)));
            var blocked = all.Where(r => r.Kind == AvailabilityKind.Exception && r.Date == date && !r.Available).Select(r => (r.Start, r.End)).ToList();

            foreach (var (start, end) in windows)
            {
                var cursor = start;
                while (cursor.AddMinutes(slotMinutes) <= end && cursor < cursor.AddMinutes(slotMinutes))
                {
                    var slotEnd = cursor.AddMinutes(slotMinutes);
                    if (!blocked.Any(b => cursor < b.End && slotEnd > b.Start))
                    {
                        var localStart = date.ToDateTime(cursor, DateTimeKind.Unspecified);
                        var localEnd = date.ToDateTime(slotEnd, DateTimeKind.Unspecified);
                        result.Add(new Slot(TimeZoneInfo.ConvertTimeToUtc(localStart, tz), TimeZoneInfo.ConvertTimeToUtc(localEnd, tz)));
                    }

                    var next = cursor.AddMinutes(slotMinutes + bufferMinutes);
                    if (next <= cursor)
                    {
                        break; // wrapped past midnight
                    }

                    cursor = next;
                }
            }
        }

        return result.OrderBy(s => s.StartUtc).DistinctBy(s => s.StartUtc).ToList();
    }
}
