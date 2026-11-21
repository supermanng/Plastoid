using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Payments.Domain;

public static class TransactionKinds
{
    public const string Charge = "charge";
    public const string Settlement = "settlement";
    public const string Sweep = "sweep";
    public const string Refund = "refund";
}

/// <summary>One money movement against a payment intent, with the journal entry it produced.</summary>
public sealed class PaymentTransaction : Entity
{
    private PaymentTransaction()
    {
    }

    private PaymentTransaction(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid PaymentIntentId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public Money Amount { get; private set; }
    public Money Fee { get; private set; }
    public string GatewayReference { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }
    public Guid? LedgerEntryId { get; private set; }

    public static PaymentTransaction Record(Guid tenantId, Guid intentId, string kind, Money amount, Money fee, string gatewayReference, DateTime occurredAt, Guid? ledgerEntryId)
        => new(NewId(), tenantId) { PaymentIntentId = intentId, Kind = kind, Amount = amount, Fee = fee, GatewayReference = gatewayReference, OccurredAt = occurredAt, LedgerEntryId = ledgerEntryId };
}
