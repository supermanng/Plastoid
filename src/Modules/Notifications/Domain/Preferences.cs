using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Notifications.Domain;

/// <summary>Per-user channel order, opt-outs and quiet hours (local time, Africa/Lagos by default).</summary>
public sealed class ChannelPreference : Entity
{
    public const string DefaultTimeZone = "Africa/Lagos";
    public static readonly IReadOnlyList<string> DefaultOrder = [Channels.Push, Channels.WhatsApp, Channels.Sms, Channels.Email];

    private ChannelPreference()
    {
    }

    private ChannelPreference(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid UserId { get; private set; }
    public List<string> ChannelOrder { get; private set; } = [.. DefaultOrder];
    public List<string> OptedOut { get; private set; } = [];
    public TimeOnly? QuietStart { get; private set; }
    public TimeOnly? QuietEnd { get; private set; }
    public string TimeZone { get; private set; } = DefaultTimeZone;

    public static ChannelPreference Default(Guid tenantId, Guid userId) => new(NewId(), tenantId) { UserId = userId };

    public Result Update(IReadOnlyList<string>? channelOrder, IReadOnlyList<string>? optedOut, TimeOnly? quietStart, TimeOnly? quietEnd, string? timeZone)
    {
        if (channelOrder is not null)
        {
            var order = channelOrder.Select(c => c.Trim().ToLowerInvariant()).Distinct().ToList();
            if (order.Any(c => !Channels.IsExternal(c)))
            {
                return Error.Validation("channel_invalid", "channelOrder may contain push, whatsapp, sms and email.");
            }

            ChannelOrder = order;
        }

        if (optedOut is not null)
        {
            var outs = optedOut.Select(c => c.Trim().ToLowerInvariant()).Distinct().ToList();
            if (outs.Any(c => !Channels.IsExternal(c)))
            {
                return Error.Validation("channel_invalid", "optedOut may contain push, whatsapp, sms and email (in-app cannot be disabled).");
            }

            OptedOut = outs;
        }

        if ((quietStart is null) != (quietEnd is null))
        {
            return Error.Validation("quiet_hours_invalid", "quietStart and quietEnd go together.");
        }

        QuietStart = quietStart;
        QuietEnd = quietEnd;
        if (timeZone is not null)
        {
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            }
            catch (TimeZoneNotFoundException)
            {
                return Error.Validation("time_zone_invalid", "Unknown IANA time zone.");
            }

            TimeZone = timeZone;
        }

        return Result.Success();
    }

    /// <summary>External channels to try, in order, skipping opt-outs. In-app is always written separately.</summary>
    public IReadOnlyList<string> ActiveChannels() => ChannelOrder.Where(c => !OptedOut.Contains(c)).ToList();

    public bool IsQuiet(DateTime utcNow) => QuietWindow.IsQuiet(QuietStart, QuietEnd, TimeZone, utcNow);

    /// <summary>When the current quiet window ends, in UTC; null when not quiet.</summary>
    public DateTime? QuietEndsAt(DateTime utcNow) => QuietWindow.EndsAt(QuietStart, QuietEnd, TimeZone, utcNow);
}

public static class QuietWindow
{
    public static bool IsQuiet(TimeOnly? start, TimeOnly? end, string timeZone, DateTime utcNow) => EndsAt(start, end, timeZone, utcNow) is not null;

    public static DateTime? EndsAt(TimeOnly? start, TimeOnly? end, string timeZone, DateTime utcNow)
    {
        if (start is not { } s || end is not { } e || s == e)
        {
            return null;
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
        var now = TimeOnly.FromDateTime(local);
        var today = DateOnly.FromDateTime(local);
        DateTime? localEnd = null;
        if (s < e)
        {
            // Same-day window, e.g. 13:00–14:00.
            if (now >= s && now < e)
            {
                localEnd = today.ToDateTime(e);
            }
        }
        else
        {
            // Overnight window, e.g. 21:00–07:00.
            if (now >= s)
            {
                localEnd = today.AddDays(1).ToDateTime(e);
            }
            else if (now < e)
            {
                localEnd = today.ToDateTime(e);
            }
        }

        return localEnd is { } l ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(l, DateTimeKind.Unspecified), zone) : null;
    }
}
