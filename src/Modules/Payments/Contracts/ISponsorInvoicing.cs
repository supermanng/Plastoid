using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Payments.Contracts;

public static class SponsorInvoiceLineKinds
{
    public const string Tuition = "tuition";
    public const string ManagementFee = "management_fee";
    public const string SeatFee = "seat_fee";
}

public sealed record SponsorInvoiceLine(string Kind, string Description, int Quantity, Money UnitAmount);

/// <summary>An invoice for a programme quote (blueprint §5: tuition, management fee and seat fee shown as separate lines; VAT on the fees).</summary>
public sealed record SponsorInvoiceRequest(Guid OrganisationId, Guid ProgrammeId, string CustomerName, IReadOnlyList<SponsorInvoiceLine> Lines, int DueDays);

public sealed record SponsorInvoiceHandle(Guid InvoiceId, string Number, Money Subtotal, Money Vat, Money Total, DateTime DueAt, string? VirtualAccountBank, string? VirtualAccountNumber, string? VirtualAccountName);

public sealed record SponsorInvoiceSummary(Guid InvoiceId, string Number, Guid OrganisationId, Guid? ProgrammeId, Money Total, Money Outstanding, string Status);

/// <summary>Settle (part of) an invoice with units already taken from the organisation's wallet: Amount is applied to the invoice, UnitsValue is the naira released from the unit liability.</summary>
public sealed record SettleInvoiceFromUnitsRequest(Guid InvoiceId, Money Amount, Money UnitsValue, Guid WalletId, string Reference);

/// <summary>In-process invoicing for Sponsorship (ApproveQuote issues the invoice in the same transaction). Joins the caller's unit of work.</summary>
public interface ISponsorInvoicing
{
    Task<Result<SponsorInvoiceHandle>> IssueAsync(SponsorInvoiceRequest request, CancellationToken cancellationToken);
    Task<SponsorInvoiceSummary?> GetAsync(Guid invoiceId, CancellationToken cancellationToken);
    Task<Result<SponsorInvoiceSummary>> SettleFromUnitsAsync(SettleInvoiceFromUnitsRequest request, CancellationToken cancellationToken);
}
