using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Payments.Domain;

/// <summary>A credit on a virtual account, unique per gateway reference (webhook retries are harmless).</summary>
public sealed class IncomingTransfer : Entity
{
    private IncomingTransfer()
    {
    }

    private IncomingTransfer(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string GatewayReference { get; private set; } = string.Empty;
    public string VirtualAccountNumber { get; private set; } = string.Empty;
    public Money Amount { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public string Outcome { get; private set; } = "unmatched";

    public static IncomingTransfer Record(Guid tenantId, string gatewayReference, string virtualAccountNumber, Money amount, DateTime receivedAt, Guid? invoiceId, string outcome)
        => new(NewId(), tenantId) { GatewayReference = gatewayReference, VirtualAccountNumber = virtualAccountNumber, Amount = amount, ReceivedAt = receivedAt, InvoiceId = invoiceId, Outcome = outcome };
}

/// <summary>Gateway webhook inbox: one row per event id, so replays are detected before any side effect.</summary>
public sealed class GatewayEvent : Entity
{
    private GatewayEvent()
    {
    }

    private GatewayEvent(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string Gateway { get; private set; } = string.Empty;
    public string EventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string Reference { get; private set; } = string.Empty;
    public DateTime ReceivedAt { get; private set; }
    public string Outcome { get; private set; } = string.Empty;

    public static GatewayEvent Record(Guid tenantId, string gateway, string eventId, string eventType, string reference, DateTime receivedAt, string outcome)
        => new(NewId(), tenantId) { Gateway = gateway, EventId = eventId, EventType = eventType, Reference = reference, ReceivedAt = receivedAt, Outcome = outcome };
}
