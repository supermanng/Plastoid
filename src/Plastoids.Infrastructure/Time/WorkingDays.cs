namespace Plastoids.Infrastructure.Time;

/// <summary>Working-day arithmetic for SLA timers (blueprint §13: 3 and 5 working days). Weekends excluded; public holidays are a later refinement.</summary>
public static class WorkingDays
{
    public static DateTime Add(DateTime from, int workingDays)
    {
        var current = from;
        var remaining = workingDays;
        while (remaining > 0)
        {
            current = current.AddDays(1);
            if (current.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                remaining--;
            }
        }

        return current;
    }
}
