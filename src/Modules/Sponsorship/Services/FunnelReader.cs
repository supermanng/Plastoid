using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;

namespace Plastoids.Modules.Sponsorship.Services;

public sealed record FunnelStage(string Stage, int Count, decimal PercentOfApplied);

/// <summary>Story F5: applied → offered → enrolled → active → completed → certified plus attendance and scores.</summary>
public sealed record FunnelView(Guid ProgrammeId, string Name, int Seats, int SeatsFilled, int SeatsOpen, IReadOnlyList<FunnelStage> Stages, int SessionsAttended, decimal AttendancePerSeat, decimal? AverageScore, DateTime RefreshedAt)
{
    public string ToCsv()
    {
        var sb = new StringBuilder("stage,count,percent_of_applied\n");
        foreach (var s in Stages)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{s.Stage},{s.Count},{s.PercentOfApplied}\n");
        }

        sb.Append(CultureInfo.InvariantCulture, $"seats_filled,{SeatsFilled},\nseats_open,{SeatsOpen},\nsessions_attended,{SessionsAttended},\naverage_score,{AverageScore?.ToString(CultureInfo.InvariantCulture) ?? string.Empty},\n");
        return sb.ToString();
    }
}

/// <summary>Builds the funnel view shared by the funnel endpoint and the impact report.</summary>
internal static class FunnelReader
{
    public static async Task<FunnelView> BuildAsync(SponsorshipDbContext db, Programme programme, CancellationToken ct)
    {
        var funnel = await db.Funnels.AsNoTracking().SingleOrDefaultAsync(f => f.ProgrammeId == programme.Id, ct);
        var seats = await db.Seats.AsNoTracking().Where(s => s.ProgrammeId == programme.Id).Select(s => new { s.Status, s.AttendanceCount, s.LastScore }).ToListAsync(ct);
        var filled = seats.Count(s => s.Status == SeatStatus.Filled);
        var open = seats.Count(s => s.Status == SeatStatus.Open);
        var attended = seats.Sum(s => s.AttendanceCount);
        var scores = seats.Where(s => s.LastScore is not null).Select(s => s.LastScore!.Value).ToList();
        var applied = funnel?.Applied ?? 0;
        decimal Pct(int n) => applied == 0 ? 0 : Math.Round(100m * n / applied, 1);
        var stages = new List<FunnelStage>
        {
            new("applied", applied, applied == 0 ? 0 : 100),
            new("offered", funnel?.Offered ?? 0, Pct(funnel?.Offered ?? 0)),
            new("enrolled", funnel?.Enrolled ?? 0, Pct(funnel?.Enrolled ?? 0)),
            new("active", funnel?.Active ?? 0, Pct(funnel?.Active ?? 0)),
            new("completed", funnel?.Completed ?? 0, Pct(funnel?.Completed ?? 0)),
            new("certified", funnel?.Certified ?? 0, Pct(funnel?.Certified ?? 0)),
        };
        return new FunnelView(programme.Id, programme.Name, programme.SeatsUnlocked, filled, open, stages, attended, filled == 0 ? 0 : Math.Round((decimal)attended / filled, 2), scores.Count == 0 ? null : Math.Round(scores.Average(), 2), funnel?.RefreshedAt ?? programme.UpdatedAt);
    }
}
