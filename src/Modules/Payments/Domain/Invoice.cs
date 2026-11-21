using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Payments.Domain;

public enum InvoiceStatus
{
    Issued = 0,
    PartiallyPaid = 1,
    Paid = 2,
    Held = 3,
}

public static class InvoiceLineKinds
{
    public const string Tuition = "tuition";
    public const string ManagementFee = "management_fee";
    public const string SeatFee = "seat_fee";
    public const string Vat = "vat";
    public static readonly IReadOnlyCollection<string> Billable = [Tuition, ManagementFee, SeatFee];
    public static bool IsBillable(string k) => Billable.Contains(k, StringComparer.Ordinal);

    /// <summary>VAT applies to Plastoids' fees, not to pass-through tuition (blueprint §18 entry 6; to be confirmed by a tax adviser).</summary>
    public static bool IsVatable(string k) => k is ManagementFee or SeatFee;
}

public sealed class InvoiceLine : Entity
{
    private InvoiceLine()
    {
    }

    private InvoiceLine(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid InvoiceId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public Money UnitAmount { get; private set; }
    public Money Amount { get; private set; }
    public int Line { get; private set; }

    public static InvoiceLine Create(Guid tenantId, Guid invoiceId, int line, string kind, string description, int quantity, Money unitAmount)
        => new(NewId(), tenantId) { InvoiceId = invoiceId, Line = line, Kind = kind, Description = description.Trim(), Quantity = quantity, UnitAmount = unitAmount, Amount = new Money(checked(unitAmount.AmountKobo * quantity), unitAmount.Currency) };
}

public enum TransferMatch
{
    Matched = 0,
    Partial = 1,
    Overpaid = 2,
}

/// <summary>Sponsor invoice (story G2): dedicated virtual account per invoice, auto-matched on credit, partial payments flagged, wrong amounts held.</summary>
[Audited]
public sealed class Invoice : Entity
{
    public const int VatBps = 750;

    private Invoice()
    {
    }

    private Invoice(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid OrganisationId { get; private set; }
    public Guid? ProgrammeId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Money Subtotal { get; private set; }
    public Money Vat { get; private set; }
    public Money Total { get; private set; }
    public Money PaidAmount { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public DateTime IssuedAt { get; private set; }
    public DateTime DueAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public string? VirtualAccountBank { get; private set; }
    public string? VirtualAccountNumber { get; private set; }
    public string? VirtualAccountName { get; private set; }
    public Guid? LedgerEntryId { get; private set; }
    public Guid CreatedBy { get; private set; }

    public static Invoice Issue(Guid tenantId, Guid organisationId, Guid? programmeId, string number, Money subtotal, Money vat, DateTime utcNow, int dueDays, Guid createdBy)
        => new(NewId(), tenantId)
        {
            OrganisationId = organisationId,
            ProgrammeId = programmeId,
            Number = number,
            Subtotal = subtotal,
            Vat = vat,
            Total = subtotal + vat,
            PaidAmount = Money.Zero(subtotal.Currency),
            Status = InvoiceStatus.Issued,
            IssuedAt = utcNow,
            DueAt = utcNow.AddDays(dueDays),
            CreatedBy = createdBy,
        };

    public void AttachVirtualAccount(string bank, string number, string name)
    {
        VirtualAccountBank = bank;
        VirtualAccountNumber = number;
        VirtualAccountName = name;
    }

    public Money Outstanding => Total - PaidAmount;

    /// <summary>Applies a credit. Exact (or completing) payment → Paid; less → PartiallyPaid; more than outstanding → Held for finance.</summary>
    public Result<TransferMatch> ApplyTransfer(Money amount, DateTime utcNow, Guid ledgerEntryId)
    {
        if (Status == InvoiceStatus.Paid)
        {
            return Error.Conflict("invoice_already_paid", "Invoice is already paid.");
        }

        if (amount > Outstanding)
        {
            Status = InvoiceStatus.Held;
            return TransferMatch.Overpaid;
        }

        PaidAmount += amount;
        LedgerEntryId = ledgerEntryId;
        if (Outstanding.IsZero)
        {
            Status = InvoiceStatus.Paid;
            PaidAt = utcNow;
            return TransferMatch.Matched;
        }

        Status = InvoiceStatus.PartiallyPaid;
        return TransferMatch.Partial;
    }

    public static Money VatOn(Money vatableAmount) => vatableAmount.Percent(VatBps / 100m, MidpointRounding.ToEven);
}
