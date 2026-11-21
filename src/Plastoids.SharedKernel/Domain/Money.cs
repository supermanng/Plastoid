using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plastoids.SharedKernel.Domain;

/// <summary>
/// Money stored as an integer amount of the smallest unit (kobo for NGN) plus an ISO 4217 code.
/// Never floating point. Arithmetic across currencies throws. On the wire it is { "amount": kobo, "currency": "NGN" }.
/// </summary>
[JsonConverter(typeof(MoneyJsonConverter))]
public readonly record struct Money : IComparable<Money>
{
    public const string Naira = "NGN";

    public Money(long amountKobo, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter ISO 4217 code.", nameof(currency));
        }

        AmountKobo = amountKobo;
        Currency = currency.ToUpperInvariant();
    }

    public long AmountKobo { get; }
    public string Currency { get; }

    public static Money Zero(string currency = Naira) => new(0, currency);
    public static Money Kobo(long amountKobo) => new(amountKobo, Naira);

    /// <summary>Build from whole naira; fractions of a naira are rejected to avoid silent rounding.</summary>
    public static Money NairaAmount(long naira) => new(checked(naira * 100), Naira);

    public bool IsZero => AmountKobo == 0;
    public bool IsNegative => AmountKobo < 0;

    public Money Add(Money other) => new(checked(AmountKobo + Same(other).AmountKobo), Currency);
    public Money Subtract(Money other) => new(checked(AmountKobo - Same(other).AmountKobo), Currency);
    public Money Negate() => new(checked(-AmountKobo), Currency);

    /// <summary>Percentage with explicit rounding (banker's rounding by default). Used for commissions and fees.</summary>
    public Money Percent(decimal percent, MidpointRounding rounding = MidpointRounding.ToEven)
    {
        var raw = AmountKobo * percent / 100m;
        return new Money((long)Math.Round(raw, 0, rounding), Currency);
    }

    /// <summary>Split into <paramref name="parts"/> shares whose sum equals the original; the remainder goes to the first shares.</summary>
    public IReadOnlyList<Money> Allocate(int parts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parts);

        var baseShare = AmountKobo / parts;
        var remainder = AmountKobo % parts;
        var result = new Money[parts];
        for (var i = 0; i < parts; i++)
        {
            result[i] = new Money(baseShare + (i < Math.Abs(remainder) ? Math.Sign(remainder) : 0), Currency);
        }

        return result;
    }

    public static Money operator +(Money a, Money b) => a.Add(b);
    public static Money operator -(Money a, Money b) => a.Subtract(b);
    public static Money operator -(Money a) => a.Negate();
    public static bool operator >(Money a, Money b) => a.CompareTo(b) > 0;
    public static bool operator <(Money a, Money b) => a.CompareTo(b) < 0;
    public static bool operator >=(Money a, Money b) => a.CompareTo(b) >= 0;
    public static bool operator <=(Money a, Money b) => a.CompareTo(b) <= 0;

    public int CompareTo(Money other) => AmountKobo.CompareTo(Same(other).AmountKobo);

    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"{Currency} {AmountKobo / 100}.{Math.Abs(AmountKobo % 100):00}");

    private Money Same(Money other)
        => other.Currency == Currency
            ? other
            : throw new InvalidOperationException($"Currency mismatch: {Currency} vs {other.Currency}.");
}

/// <summary>Blueprint §17: money as { "amount": 4500000, "currency": "NGN" } (amount in kobo).</summary>
public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Money must be an object { amount, currency }.");
        }

        long? amount = null;
        string? currency = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            if (string.Equals(name, "amount", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "amountKobo", StringComparison.OrdinalIgnoreCase))
            {
                amount = reader.GetInt64();
            }
            else if (string.Equals(name, "currency", StringComparison.OrdinalIgnoreCase))
            {
                currency = reader.GetString();
            }
            else
            {
                reader.Skip();
            }
        }

        if (amount is null)
        {
            throw new JsonException("Money needs an integer 'amount' in kobo.");
        }

        return new Money(amount.Value, currency ?? Money.Naira);
    }

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("amount", value.AmountKobo);
        writer.WriteString("currency", value.Currency);
        writer.WriteEndObject();
    }
}
