using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Infrastructure.Integrations;

public sealed record CheckoutSession(string GatewayReference, Uri CheckoutUrl);

public sealed record VirtualAccount(string BankName, string AccountNumber, string AccountName);

public sealed record GatewayTransactionStatus(string Reference, string Status, Money Amount, Money Fee, DateTime? PaidAt);

/// <summary>A parsed gateway webhook. EventType is normalised: charge.success | charge.failed | virtual_account.credit | refund.processed.</summary>
public sealed record GatewayWebhook(string EventId, string EventType, string Reference, Money Amount, Money Fee, DateTime OccurredAt, string? VirtualAccountNumber, string? PayerEmail);

/// <summary>Hosted-checkout payment gateway (blueprint §17: Paystack primary, Flutterwave fallback). Fake locally.</summary>
public interface IPaymentGateway
{
    string Name { get; }
    Task<Result<CheckoutSession>> InitialiseCheckoutAsync(Guid paymentIntentId, Money amount, string payerEmail, Uri callbackUrl, CancellationToken cancellationToken);
    Task<Result<GatewayTransactionStatus>> VerifyAsync(string gatewayReference, CancellationToken cancellationToken);
    bool VerifyWebhookSignature(string payload, string signature);
    Result<GatewayWebhook> ParseWebhook(string payload);
    Task<Result<VirtualAccount>> CreateVirtualAccountAsync(string invoiceNumber, string customerName, CancellationToken cancellationToken);
    Task<Result<string>> RefundAsync(string gatewayReference, Money amount, string reason, CancellationToken cancellationToken);
}

/// <summary>
/// Deterministic fake: checkout URLs point at a local page, webhooks are HMAC-SHA512 signed with Secrets:PaymentWebhookSecret
/// (default "fake-payment-webhook-secret"), fees follow Paystack's local pricing (1.5% + ₦100 above ₦2,500, capped at ₦2,000).
/// </summary>
public sealed class FakePaymentGateway(Secrets.ISecretProvider secrets) : IPaymentGateway
{
    public const string WebhookSecretName = "PaymentWebhookSecret";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, GatewayTransactionStatus> _transactions = new();

    public string Name => "fake";

    public Task<Result<CheckoutSession>> InitialiseCheckoutAsync(Guid paymentIntentId, Money amount, string payerEmail, Uri callbackUrl, CancellationToken cancellationToken)
    {
        var reference = "fk_" + paymentIntentId.ToString("N")[..20];
        _transactions[reference] = new GatewayTransactionStatus(reference, "pending", amount, Money.Zero(amount.Currency), null);
        return Task.FromResult(Result.Success(new CheckoutSession(reference, new Uri($"https://checkout.plastoids.local/pay/{reference}?callback={Uri.EscapeDataString(callbackUrl.ToString())}"))));
    }

    /// <summary>Tests call this to simulate the customer paying on the hosted page.</summary>
    public void SimulateCharge(string reference, bool success, DateTime paidAt)
    {
        if (_transactions.TryGetValue(reference, out var t))
        {
            _transactions[reference] = t with { Status = success ? "success" : "failed", Fee = success ? Fee(t.Amount) : Money.Zero(t.Amount.Currency), PaidAt = success ? paidAt : null };
        }
    }

    public Task<Result<GatewayTransactionStatus>> VerifyAsync(string gatewayReference, CancellationToken cancellationToken)
        => Task.FromResult(_transactions.TryGetValue(gatewayReference, out var t)
            ? Result.Success(t)
            : Result.Failure<GatewayTransactionStatus>(Error.NotFound("gateway_reference_unknown", "Unknown gateway reference.")));

    public bool VerifyWebhookSignature(string payload, string signature)
        => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(payload)), Encoding.ASCII.GetBytes(signature ?? string.Empty));

    public string Sign(string payload)
        => Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secrets.Get(WebhookSecretName) ?? "fake-payment-webhook-secret"), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    public Result<GatewayWebhook> ParseWebhook(string payload)
    {
        try
        {
            var w = JsonSerializer.Deserialize<GatewayWebhook>(payload, Json);
            return w is null ? Error.Validation("webhook_payload_invalid", "Body must be a webhook JSON object.") : w;
        }
        catch (JsonException)
        {
            return Error.Validation("webhook_payload_invalid", "Body must be a webhook JSON object.");
        }
    }

    /// <summary>Builds a signed webhook body for a reference the fake knows (tests and local tooling).</summary>
    public (string Payload, string Signature) BuildWebhook(string eventType, string reference, Money amount, DateTime occurredAt, string? virtualAccountNumber = null, string? eventId = null)
    {
        var fee = eventType == "charge.success" ? Fee(amount) : Money.Zero(amount.Currency);
        var payload = JsonSerializer.Serialize(new GatewayWebhook(eventId ?? $"evt_{reference}_{eventType}", eventType, reference, amount, fee, occurredAt, virtualAccountNumber, null), Json);
        return (payload, Sign(payload));
    }

    public Task<Result<VirtualAccount>> CreateVirtualAccountAsync(string invoiceNumber, string customerName, CancellationToken cancellationToken)
    {
        var digits = Math.Abs(invoiceNumber.GetHashCode(StringComparison.Ordinal)) % 1_000_000_000;
        return Task.FromResult(Result.Success(new VirtualAccount("Wema Bank (fake)", "9" + digits.ToString("D9", System.Globalization.CultureInfo.InvariantCulture), $"PLASTOIDS - {customerName.ToUpperInvariant()}")));
    }

    public Task<Result<string>> RefundAsync(string gatewayReference, Money amount, string reason, CancellationToken cancellationToken)
        => Task.FromResult(_transactions.ContainsKey(gatewayReference)
            ? Result.Success("rf_" + Guid.CreateVersion7().ToString("N")[..16])
            : Result.Failure<string>(Error.NotFound("gateway_reference_unknown", "Unknown gateway reference.")));

    /// <summary>Paystack local card pricing: 1.5% + ₦100 (waived under ₦2,500), capped at ₦2,000.</summary>
    public static Money Fee(Money amount)
    {
        if (amount.Currency != Money.Naira)
        {
            return Money.Zero(amount.Currency);
        }

        var fee = amount.Percent(1.5m, MidpointRounding.AwayFromZero);
        if (amount.AmountKobo >= 250_000)
        {
            fee += Money.NairaAmount(100);
        }

        return fee > Money.NairaAmount(2000) ? Money.NairaAmount(2000) : fee;
    }
}

/// <summary>Paystack adapter: initialize, verify, webhook (HMAC-SHA512 of the raw body with the secret key), dedicated virtual accounts, refunds.</summary>
internal sealed class PaystackGateway(HttpClient http, Secrets.ISecretProvider secrets) : IPaymentGateway
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public string Name => "paystack";

    private string Secret => secrets.GetRequired("PaystackSecretKey");

    public async Task<Result<CheckoutSession>> InitialiseCheckoutAsync(Guid paymentIntentId, Money amount, string payerEmail, Uri callbackUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.paystack.co/transaction/initialize");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Secret);
        request.Content = new StringContent(JsonSerializer.Serialize(new { email = payerEmail, amount = amount.AmountKobo, currency = amount.Currency, reference = "ps_" + paymentIntentId.ToString("N"), callback_url = callbackUrl.ToString() }), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Error.Failure("gateway_error", $"Paystack initialize failed ({(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        return new CheckoutSession(data.GetProperty("reference").GetString()!, new Uri(data.GetProperty("authorization_url").GetString()!));
    }

    public async Task<Result<GatewayTransactionStatus>> VerifyAsync(string gatewayReference, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.paystack.co/transaction/verify/{Uri.EscapeDataString(gatewayReference)}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Secret);
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Error.Failure("gateway_error", $"Paystack verify failed ({(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        var currency = data.GetProperty("currency").GetString() ?? Money.Naira;
        return new GatewayTransactionStatus(gatewayReference, data.GetProperty("status").GetString() ?? "unknown",
            new Money(data.GetProperty("amount").GetInt64(), currency), new Money(data.TryGetProperty("fees", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetInt64() : 0, currency),
            data.TryGetProperty("paid_at", out var p) && p.ValueKind == JsonValueKind.String ? DateTime.Parse(p.GetString()!, null, System.Globalization.DateTimeStyles.AdjustToUniversal) : null);
    }

    public bool VerifyWebhookSignature(string payload, string signature)
    {
        var expected = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes((signature ?? string.Empty).ToLowerInvariant()));
    }

    public Result<GatewayWebhook> ParseWebhook(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var evt = root.GetProperty("event").GetString() ?? string.Empty;
        var data = root.GetProperty("data");
        var currency = data.TryGetProperty("currency", out var c) ? c.GetString() ?? Money.Naira : Money.Naira;
        var amount = new Money(data.GetProperty("amount").GetInt64(), currency);
        var fee = new Money(data.TryGetProperty("fees", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetInt64() : 0, currency);
        var reference = data.GetProperty("reference").GetString() ?? string.Empty;
        var type = evt switch
        {
            "charge.success" when data.TryGetProperty("channel", out var ch) && ch.GetString() == "dedicated_nuban" => "virtual_account.credit",
            "charge.success" => "charge.success",
            "charge.failed" => "charge.failed",
            "refund.processed" => "refund.processed",
            _ => evt,
        };
        string? virtualAccount = data.TryGetProperty("authorization", out var auth) && auth.TryGetProperty("receiver_bank_account_number", out var acct) ? acct.GetString() : null;
        var occurred = data.TryGetProperty("paid_at", out var p) && p.ValueKind == JsonValueKind.String ? DateTime.Parse(p.GetString()!, null, System.Globalization.DateTimeStyles.AdjustToUniversal) : DateTime.UnixEpoch;
        return new GatewayWebhook($"{evt}:{reference}", type, reference, amount, fee, occurred, virtualAccount, data.TryGetProperty("customer", out var cu) && cu.TryGetProperty("email", out var em) ? em.GetString() : null);
    }

    public Task<Result<VirtualAccount>> CreateVirtualAccountAsync(string invoiceNumber, string customerName, CancellationToken cancellationToken)
        => Task.FromResult(Result.Failure<VirtualAccount>(Error.Failure("gateway_not_implemented", "Paystack dedicated virtual accounts need a customer code; wire /dedicated_account when the account is live.")));

    public Task<Result<string>> RefundAsync(string gatewayReference, Money amount, string reason, CancellationToken cancellationToken)
        => Task.FromResult(Result.Failure<string>(Error.Failure("gateway_not_implemented", "Paystack /refund call is a stub.")));
}

internal sealed class FlutterwaveGateway(Secrets.ISecretProvider secrets) : IPaymentGateway
{
    public string Name => "flutterwave";
    private Result<T> NotImplemented<T>() { _ = secrets.GetRequired("FlutterwaveSecretKey"); return Error.Failure("gateway_not_implemented", "Flutterwave adapter is a stub."); }
    public Task<Result<CheckoutSession>> InitialiseCheckoutAsync(Guid paymentIntentId, Money amount, string payerEmail, Uri callbackUrl, CancellationToken cancellationToken) => Task.FromResult(NotImplemented<CheckoutSession>());
    public Task<Result<GatewayTransactionStatus>> VerifyAsync(string gatewayReference, CancellationToken cancellationToken) => Task.FromResult(NotImplemented<GatewayTransactionStatus>());
    public bool VerifyWebhookSignature(string payload, string signature) => false;
    public Result<GatewayWebhook> ParseWebhook(string payload) => NotImplemented<GatewayWebhook>();
    public Task<Result<VirtualAccount>> CreateVirtualAccountAsync(string invoiceNumber, string customerName, CancellationToken cancellationToken) => Task.FromResult(NotImplemented<VirtualAccount>());
    public Task<Result<string>> RefundAsync(string gatewayReference, Money amount, string reason, CancellationToken cancellationToken) => Task.FromResult(NotImplemented<string>());
}
