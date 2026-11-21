using System.Security.Cryptography;
using System.Text;
using Plastoids.Infrastructure.Security;
using Plastoids.Infrastructure.Sms;
using Plastoids.SharedKernel.Results;
using StackExchange.Redis;

namespace Plastoids.Modules.Identity.Services;

/// <summary>
/// Phone OTP (story A1): 6 digits, 10 min TTL, 5 requests per phone per hour and 5 wrong guesses per code, all in Redis.
/// Only a SHA-256 of the code is stored.
/// </summary>
internal sealed class OtpService(IConnectionMultiplexer redis, ISmsSender sms, IFieldEncryptor encryptor)
{
    public static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(10);
    public const int MaxRequestsPerHour = 5;
    public const int MaxWrongGuesses = 5;

    public async Task<Result<TimeSpan>> RequestAsync(string phoneE164, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var hash = encryptor.BlindIndex(phoneE164);
        var requestsKey = $"identity:otp:req:{hash}";

        var requests = await db.StringIncrementAsync(requestsKey);
        if (requests == 1)
        {
            await db.KeyExpireAsync(requestsKey, TimeSpan.FromHours(1));
        }

        if (requests > MaxRequestsPerHour)
        {
            return Error.RateLimited("otp_rate_limited", "Too many codes requested for this number. Try again in an hour.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        await db.StringSetAsync($"identity:otp:code:{hash}", Hash(code), CodeTtl);
        await db.KeyDeleteAsync($"identity:otp:fail:{hash}");
        await sms.SendAsync(phoneE164, $"Your Plastoids code is {code}. It expires in {CodeTtl.TotalMinutes:0} minutes. Never share it.", cancellationToken);
        return CodeTtl;
    }

    /// <summary>Consumes the code on success. Wrong guesses are counted; after five the code is invalidated.</summary>
    public async Task<Result> VerifyAsync(string phoneE164, string code)
    {
        var db = redis.GetDatabase();
        var hash = encryptor.BlindIndex(phoneE164);
        var codeKey = $"identity:otp:code:{hash}";
        var failKey = $"identity:otp:fail:{hash}";

        var stored = await db.StringGetAsync(codeKey);
        if (stored.IsNullOrEmpty)
        {
            return Error.Validation("otp_expired", "No active code for this number; request a new one.");
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(stored!), Encoding.ASCII.GetBytes(Hash(code))))
        {
            var failures = await db.StringIncrementAsync(failKey);
            await db.KeyExpireAsync(failKey, CodeTtl);
            if (failures >= MaxWrongGuesses)
            {
                await db.KeyDeleteAsync(codeKey);
                return Error.Validation("otp_locked", "Too many wrong codes; request a new one.");
            }

            return Error.Validation("otp_invalid", "The code is incorrect.");
        }

        await db.KeyDeleteAsync([new RedisKey(codeKey), new RedisKey(failKey)]);
        return Result.Success();
    }

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(code)));
}
