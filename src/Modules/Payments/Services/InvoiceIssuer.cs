using Plastoids.SharedKernel.Messaging;
using Plastoids.Modules.Ledger.Contracts;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payments.Services;

public sealed record IssuedLine(int Line, string Kind, string Description, int Quantity, Money UnitAmount, Money Amount);

/// <summary>Issues sponsor invoices (story G2): sums the lines, adds VAT on the fee lines, numbers the invoice and attaches a dedicated virtual account.</summary>
internal sealed class InvoiceIssuer(PaymentsDbContext db, IPaymentGateway gateway, ICurrentTenant tenant, IClock clock)
{
    public async Task<Result<(Invoice Invoice, IReadOnlyList<IssuedLine> Lines)>> IssueAsync(Guid organisationId, Guid? programmeId, string customerName, IReadOnlyList<(string Kind, string Description, int Quantity, Money UnitAmount)> lines, int dueDays, Guid createdBy, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var subtotal = Money.Zero();
        var vatable = Money.Zero();
        foreach (var line in lines)
        {
            if (!InvoiceLineKinds.IsBillable(line.Kind))
            {
                return Error.Validation("invoice_line_kind_invalid", "kind must be tuition, management_fee or seat_fee.");
            }

            var amount = new Money(checked(line.UnitAmount.AmountKobo * line.Quantity), line.UnitAmount.Currency);
            subtotal += amount;
            if (InvoiceLineKinds.IsVatable(line.Kind))
            {
                vatable += amount;
            }
        }

        var vat = Invoice.VatOn(vatable);
        var sequence = await db.Invoices.CountAsync(i => i.IssuedAt.Year == now.Year, ct) + 1;
        var number = $"INV-{now:yyyy}-{sequence:D5}";
        var invoice = Invoice.Issue(tenant.TenantId, organisationId, programmeId, number, subtotal, vat, now, dueDays, createdBy);
        var account = await gateway.CreateVirtualAccountAsync(number, customerName, ct);
        if (account.IsFailure)
        {
            return Result.Failure<(Invoice, IReadOnlyList<IssuedLine>)>(account.Error);
        }

        invoice.AttachVirtualAccount(account.Value.BankName, account.Value.AccountNumber, account.Value.AccountName);
        db.Invoices.Add(invoice);
        var issued = new List<IssuedLine>();
        var n = 0;
        foreach (var line in lines)
        {
            var row = InvoiceLine.Create(tenant.TenantId, invoice.Id, ++n, line.Kind, line.Description, line.Quantity, line.UnitAmount);
            db.InvoiceLines.Add(row);
            issued.Add(new IssuedLine(row.Line, row.Kind, row.Description, row.Quantity, row.UnitAmount, row.Amount));
        }

        if (!vat.IsZero)
        {
            var vatRow = InvoiceLine.Create(tenant.TenantId, invoice.Id, ++n, InvoiceLineKinds.Vat, "VAT 7.5% on fees", 1, vat);
            db.InvoiceLines.Add(vatRow);
            issued.Add(new IssuedLine(vatRow.Line, vatRow.Kind, vatRow.Description, 1, vat, vat));
        }

        return (invoice, issued);
    }
}

/// <summary>Implementation of <see cref="ISponsorInvoicing"/>: enlists the Payments context in the caller's unit of work.</summary>
internal sealed class SponsorInvoicing(PaymentsDbContext db, IUnitOfWork unitOfWork, InvoiceIssuer issuer, ILedgerService ledger, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ISponsorInvoicing
{
    public async Task<SponsorInvoiceSummary?> GetAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        var invoice = await db.Invoices.AsNoTracking().SingleOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        return invoice is null ? null : Summary(invoice);
    }

    /// <summary>Units already taken from the organisation's wallet settle the invoice: entry 6 (units variant), then the same invoice-paid event as a transfer.</summary>
    public async Task<Result<SponsorInvoiceSummary>> SettleFromUnitsAsync(SettleInvoiceFromUnitsRequest request, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        var invoice = await db.Invoices.SingleOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Error.NotFound("invoice_not_found", "No such invoice.");
        }

        if (request.Amount.IsNegative || request.Amount.IsZero || request.Amount > invoice.Outstanding)
        {
            return Error.Validation("invoice_amount_invalid", $"Amount must be positive and at most the outstanding {invoice.Outstanding}.");
        }

        var now = clock.UtcNow;
        var entry = await ledger.PostAsync(PaymentPostings.Entry6InvoicePaidFromUnits(invoice, request.Amount, request.UnitsValue, request.WalletId, now), cancellationToken);
        if (entry.IsFailure)
        {
            return Result.Failure<SponsorInvoiceSummary>(entry.Error);
        }

        var match = invoice.ApplyTransfer(request.Amount, now, entry.Value);
        if (match.IsFailure)
        {
            return Result.Failure<SponsorInvoiceSummary>(match.Error);
        }

        db.Transfers.Add(IncomingTransfer.Record(invoice.TenantId, request.Reference, invoice.VirtualAccountNumber ?? "units", request.Amount, now, invoice.Id, match.Value == TransferMatch.Matched ? "matched" : "partial"));
        if (match.Value == TransferMatch.Matched)
        {
            outbox.Enqueue(new InvoicePaid(invoice.Id, invoice.Id, invoice.OrganisationId, invoice.ProgrammeId, invoice.Total));
        }

        return Summary(invoice);
    }

    private static SponsorInvoiceSummary Summary(Invoice invoice) => new(invoice.Id, invoice.Number, invoice.OrganisationId, invoice.ProgrammeId, invoice.Total, invoice.Outstanding, invoice.Status.ToString().ToLowerInvariant());

    public async Task<Result<SponsorInvoiceHandle>> IssueAsync(SponsorInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        var lines = request.Lines.Select(l => (l.Kind, l.Description, l.Quantity, l.UnitAmount)).ToList();
        var issued = await issuer.IssueAsync(request.OrganisationId, request.ProgrammeId, request.CustomerName, lines, request.DueDays, currentUser.UserId ?? Guid.Empty, cancellationToken);
        if (issued.IsFailure)
        {
            return Result.Failure<SponsorInvoiceHandle>(issued.Error);
        }

        var invoice = issued.Value.Invoice;
        return new SponsorInvoiceHandle(invoice.Id, invoice.Number, invoice.Subtotal, invoice.Vat, invoice.Total, invoice.DueAt, invoice.VirtualAccountBank, invoice.VirtualAccountNumber, invoice.VirtualAccountName);
    }
}
