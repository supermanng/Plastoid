using StackExchange.Redis;

namespace Plastoids.Modules.Booking.Services;

/// <summary>Blueprint §9 edge case: the slot is locked for 10 minutes at checkout so calendar-sync lag cannot double-book it.</summary>
internal sealed class SlotLocks(IConnectionMultiplexer redis)
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(10);

    /// <summary>Acquires or renews the lock for this learner. Returns false when another learner holds it.</summary>
    public async Task<bool> TryLockAsync(Guid tutorId, DateTime slotStart, Guid learnerId)
    {
        var db = redis.GetDatabase();
        var key = Key(tutorId, slotStart);
        var token = learnerId.ToString("N");
        if (await db.StringSetAsync(key, token, Duration, When.NotExists))
        {
            return true;
        }

        var holder = await db.StringGetAsync(key);
        if (holder == token)
        {
            await db.KeyExpireAsync(key, Duration);
            return true;
        }

        return false;
    }

    public async Task<bool> IsHeldByAsync(Guid tutorId, DateTime slotStart, Guid learnerId)
    {
        var holder = await redis.GetDatabase().StringGetAsync(Key(tutorId, slotStart));
        return holder == learnerId.ToString("N");
    }

    public Task ReleaseAsync(Guid tutorId, DateTime slotStart) => redis.GetDatabase().KeyDeleteAsync(Key(tutorId, slotStart));

    public async Task<TimeSpan?> RemainingAsync(Guid tutorId, DateTime slotStart) => await redis.GetDatabase().KeyTimeToLiveAsync(Key(tutorId, slotStart));

    private static string Key(Guid tutorId, DateTime slotStart) => $"booking:lock:{tutorId:N}:{slotStart:yyyyMMddHHmm}";
}
