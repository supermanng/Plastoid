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

/// <summary>In-process invoicing for Sponsorship (ApproveQuote issues the invoice in the same transaction). Joins the caller's unit of work.</summary>
public interface ISponsorInvoicing
{
    Task<Result<SponsorInvoiceHandle>> IssueAsync(SponsorInvoiceRequest request, CancellationToken cancellationToken);
}
