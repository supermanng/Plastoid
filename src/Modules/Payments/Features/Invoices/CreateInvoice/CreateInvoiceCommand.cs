using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payments.Features.Invoices.CreateInvoice;

public sealed record InvoiceLineInput(string Kind, string Description, int Quantity, Money UnitAmount);

/// <summary>Sponsor invoice: tuition, management fee and seat fee lines; VAT (7.5%) is added on the fee lines; a dedicated virtual account is attached.</summary>
public sealed record CreateInvoiceCommand(Guid OrganisationId, Guid? ProgrammeId, string CustomerName, IReadOnlyList<InvoiceLineInput> Lines, int DueDays, string IdempotencyKey) : ICommand<InvoiceView>, IIdempotentCommand;

public sealed record CreateInvoiceRequest(Guid? ProgrammeId, string CustomerName, IReadOnlyList<InvoiceLineInput> Lines, int? DueDays);

public sealed record InvoiceLineView(int Line, string Kind, string Description, int Quantity, Money UnitAmount, Money Amount);

public sealed record InvoiceView(Guid InvoiceId, string Number, Guid OrganisationId, Guid? ProgrammeId, string Status, Money Subtotal, Money Vat, Money Total, Money PaidAmount, DateTime IssuedAt, DateTime DueAt, DateTime? PaidAt, string? VirtualAccountBank, string? VirtualAccountNumber, string? VirtualAccountName, IReadOnlyList<InvoiceLineView> Lines);

internal sealed class CreateInvoiceValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Lines).NotEmpty().Must(l => l.Count <= 50);
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Kind).Must(InvoiceLineKinds.IsBillable).WithMessage("kind must be tuition, management_fee or seat_fee (VAT is computed).");
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
            l.RuleFor(x => x.Quantity).InclusiveBetween(1, 100_000);
            l.RuleFor(x => x.UnitAmount.AmountKobo).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.UnitAmount.Currency).Equal("NGN");
        });
        RuleFor(x => x.DueDays).InclusiveBetween(1, 90);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class CreateInvoiceHandler(PaymentsDbContext db, IPaymentGateway gateway, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock) : ICommandHandler<CreateInvoiceCommand, InvoiceView>
{
    public async Task<Result<InvoiceView>> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var subtotal = Money.Zero();
        var vatable = Money.Zero();
        foreach (var line in request.Lines)
        {
            var amount = new Money(checked(line.UnitAmount.AmountKobo * line.Quantity), line.UnitAmount.Currency);
            subtotal += amount;
            if (InvoiceLineKinds.IsVatable(line.Kind))
            {
                vatable += amount;
            }
        }

        var vat = Invoice.VatOn(vatable);
        var sequence = await db.Invoices.CountAsync(i => i.IssuedAt.Year == now.Year, cancellationToken) + 1;
        var number = $"INV-{now:yyyy}-{sequence:D5}";
        var invoice = Invoice.Issue(tenant.TenantId, request.OrganisationId, request.ProgrammeId, number, subtotal, vat, now, request.DueDays, currentUser.UserId ?? Guid.Empty);

        var account = await gateway.CreateVirtualAccountAsync(number, request.CustomerName, cancellationToken);
        if (account.IsFailure)
        {
            return Result.Failure<InvoiceView>(account.Error);
        }

        invoice.AttachVirtualAccount(account.Value.BankName, account.Value.AccountNumber, account.Value.AccountName);
        db.Invoices.Add(invoice);

        var lines = new List<InvoiceLineView>();
        var n = 0;
        foreach (var line in request.Lines)
        {
            var row = InvoiceLine.Create(tenant.TenantId, invoice.Id, ++n, line.Kind, line.Description, line.Quantity, line.UnitAmount);
            db.InvoiceLines.Add(row);
            lines.Add(new InvoiceLineView(row.Line, row.Kind, row.Description, row.Quantity, row.UnitAmount, row.Amount));
        }

        if (!vat.IsZero)
        {
            var vatRow = InvoiceLine.Create(tenant.TenantId, invoice.Id, ++n, InvoiceLineKinds.Vat, "VAT 7.5% on fees", 1, vat);
            db.InvoiceLines.Add(vatRow);
            lines.Add(new InvoiceLineView(vatRow.Line, vatRow.Kind, vatRow.Description, 1, vat, vat));
        }

        return new InvoiceView(invoice.Id, invoice.Number, invoice.OrganisationId, invoice.ProgrammeId, invoice.Status.ToString().ToLowerInvariant(), invoice.Subtotal, invoice.Vat, invoice.Total, invoice.PaidAmount,
            invoice.IssuedAt, invoice.DueAt, invoice.PaidAt, invoice.VirtualAccountBank, invoice.VirtualAccountNumber, invoice.VirtualAccountName, lines);
    }
}

internal static class CreateInvoiceEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/organisations/{organisationId:guid}/invoices", async (Guid organisationId, CreateInvoiceRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new CreateInvoiceCommand(organisationId, body.ProgrammeId, body.CustomerName, body.Lines, body.DueDays ?? 14, key), ct))
                    .ToCreatedResult(r => $"/api/v1/payments/organisations/{r.OrganisationId}/invoices/{r.InvoiceId}");
            })
            .WithName("CreateInvoice")
            .WithSummary("Sponsor owner/finance: issue an invoice (tuition, management_fee, seat_fee lines; VAT added) with a dedicated virtual account. Requires Idempotency-Key.")
            .RequireAuthorization(IdentityPolicies.OrganisationFinance);
    }
}
