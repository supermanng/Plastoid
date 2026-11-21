using System.Security.Cryptography;
using System.Text.Json;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Time;
using StackExchange.Redis;

namespace Plastoids.Modules.Catalogue.Services;

public sealed record PriceQuote(string QuoteId, Guid OfferingId, Money Price, DateTime ValidUntil);

/// <summary>Blueprint §9.2: a price shown to a learner is honoured for 30 minutes. Quotes live in Redis; Payments redeems them in Sprint 3.</summary>
public sealed class PriceQuotes(IConnectionMultiplexer redis, IClock clock)
{
    public static readonly TimeSpan Validity = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<PriceQuote> IssueAsync(Guid offeringId, Money price, Guid? userId)
    {
        var id = "q_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        var quote = new PriceQuote(id, offeringId, price, clock.UtcNow.Add(Validity));
        await redis.GetDatabase().StringSetAsync(Key(id), JsonSerializer.Serialize(new Stored(offeringId, price.AmountKobo, price.Currency, userId, quote.ValidUntil), Json), Validity);
        return quote;
    }

    public async Task<PriceQuote?> GetAsync(string quoteId)
    {
        var raw = await redis.GetDatabase().StringGetAsync(Key(quoteId));
        if (raw.IsNullOrEmpty)
        {
            return null;
        }

        var stored = JsonSerializer.Deserialize<Stored>(raw!, Json)!;
        return new PriceQuote(quoteId, stored.OfferingId, new Money(stored.AmountKobo, stored.Currency), stored.ValidUntil);
    }

    private static string Key(string id) => $"catalogue:quote:{id}";

    private sealed record Stored(Guid OfferingId, long AmountKobo, string Currency, Guid? UserId, DateTime ValidUntil);
}
