using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Infrastructure.Integrations;

public sealed record TransferInstruction(Guid PayoutId, string BankCode, string AccountNumber, string AccountName, Money Amount, string Narration);

/// <summary>Status is pending | settled | failed. Most providers settle asynchronously and confirm through the webhook.</summary>
public sealed record TransferReceipt(Guid PayoutId, string ProviderReference, string Status);

/// <summary>transfer.success | transfer.failed | transfer.reversed. Reference is the provider reference returned at submission.</summary>
public sealed record PayoutWebhook(string EventId, string EventType, string Reference, Money Amount, DateTime OccurredAt, string? BankReference, string? Reason);

/// <summary>Outbound transfers for tutor payouts (blueprint §18 settlement rules): gateway transfers with a bank bulk-file fallback.</summary>
public interface IPayoutProvider
{
    string Name { get; }
    Task<Result<IReadOnlyList<TransferReceipt>>> SubmitAsync(IReadOnlyList<TransferInstruction> transfers, CancellationToken cancellationToken);
    bool VerifySignature(string payload, string? signature);
    Result<PayoutWebhook> ParseWebhook(string payload);
}

/// <summary>Fake: every transfer is accepted as pending with reference "trf_&lt;payoutId&gt;"; tests drive settlement with <see cref="BuildWebhook"/>.</summary>
public sealed class FakePayoutProvider(Secrets.ISecretProvider secrets) : IPayoutProvider
{
    public const string WebhookSecretName = "PayoutWebhookSecret";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Name => "fake";

    public List<TransferInstruction> Submitted { get; } = [];

    public Task<Result<IReadOnlyList<TransferReceipt>>> SubmitAsync(IReadOnlyList<TransferInstruction> transfers, CancellationToken cancellationToken)
    {
        var receipts = new List<TransferReceipt>();
        foreach (var transfer in transfers)
        {
            Submitted.Add(transfer);
            // Account numbers ending in 9999 are rejected at submission, so batches can exercise the failure path without a webhook.
            receipts.Add(new TransferReceipt(transfer.PayoutId, "trf_" + transfer.PayoutId.ToString("N"), transfer.AccountNumber.EndsWith("9999", StringComparison.Ordinal) ? "failed" : "pending"));
        }

        return Task.FromResult(Result.Success<IReadOnlyList<TransferReceipt>>(receipts));
    }

    public bool VerifySignature(string payload, string? signature)
        => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(payload)), Encoding.ASCII.GetBytes(signature ?? string.Empty));

    public string Sign(string payload)
        => Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secrets.Get(WebhookSecretName) ?? "fake-payout-webhook-secret"), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    public Result<PayoutWebhook> ParseWebhook(string payload)
    {
        try
        {
            var webhook = JsonSerializer.Deserialize<PayoutWebhook>(payload, Json);
            return webhook is null || string.IsNullOrWhiteSpace(webhook.Reference) ? Error.Validation("payout_webhook_invalid", "Malformed payout webhook.") : webhook;
        }
        catch (JsonException)
        {
            return Error.Validation("payout_webhook_invalid", "Malformed payout webhook.");
        }
    }

    public (string Payload, string Signature) BuildWebhook(string eventType, string reference, Money amount, DateTime occurredAt, string? bankReference = null, string? reason = null, string? eventId = null)
    {
        var payload = JsonSerializer.Serialize(new PayoutWebhook(eventId ?? $"evt_{reference}_{eventType}", eventType, reference, amount, occurredAt, bankReference ?? "NIP" + occurredAt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture), reason), Json);
        return (payload, Sign(payload));
    }
}

/// <summary>Paystack Transfers API adapter (stub until credentials exist): transfer recipients + bulk transfers, webhook signature x-paystack-signature.</summary>
internal sealed class PaystackTransferProvider(Secrets.ISecretProvider secrets) : IPayoutProvider
{
    public string Name => "paystack";

    public Task<Result<IReadOnlyList<TransferReceipt>>> SubmitAsync(IReadOnlyList<TransferInstruction> transfers, CancellationToken cancellationToken)
    {
        _ = secrets.GetRequired("PaystackSecretKey");
        return Task.FromResult(Result.Failure<IReadOnlyList<TransferReceipt>>(Error.Failure("payout_provider_not_implemented", "Paystack transfers are a stub.")));
    }

    public bool VerifySignature(string payload, string? signature)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secrets.GetRequired("PaystackSecretKey")), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()),
            Encoding.ASCII.GetBytes(signature ?? string.Empty));

    public Result<PayoutWebhook> ParseWebhook(string payload) => Error.Failure("payout_provider_not_implemented", "Paystack transfer webhooks are a stub.");
}

/// <summary>Bank bulk-file fallback (NIBSS-style CSV handed to the partner bank): transfers are "pending" until finance uploads the bank's result file as webhooks.</summary>
public sealed class BankBulkFilePayoutProvider : IPayoutProvider
{
    public string Name => "bankfile";

    public List<string> Files { get; } = [];

    public Task<Result<IReadOnlyList<TransferReceipt>>> SubmitAsync(IReadOnlyList<TransferInstruction> transfers, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder("beneficiary_bank,account_number,account_name,amount_kobo,narration,reference\n");
        var receipts = new List<TransferReceipt>();
        foreach (var t in transfers)
        {
            var reference = "bulk_" + t.PayoutId.ToString("N");
            sb.Append(CultureInfo.InvariantCulture, $"{t.BankCode},{t.AccountNumber},\"{t.AccountName}\",{t.Amount.AmountKobo},\"{t.Narration}\",{reference}\n");
            receipts.Add(new TransferReceipt(t.PayoutId, reference, "pending"));
        }

        Files.Add(sb.ToString());
        return Task.FromResult(Result.Success<IReadOnlyList<TransferReceipt>>(receipts));
    }

    public bool VerifySignature(string payload, string? signature) => false; // result files are uploaded by finance, never pushed

    public Result<PayoutWebhook> ParseWebhook(string payload) => Error.Validation("payout_webhook_unsupported", "The bank file provider has no webhook; upload the result file instead.");
}
