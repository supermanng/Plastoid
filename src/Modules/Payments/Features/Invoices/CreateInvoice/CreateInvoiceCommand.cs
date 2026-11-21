using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

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

internal sealed class CreateInvoiceHandler(InvoiceIssuer issuer, ICurrentUser currentUser) : ICommandHandler<CreateInvoiceCommand, InvoiceView>
{
    public async Task<Result<InvoiceView>> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var lines = request.Lines.Select(l => (l.Kind, l.Description, l.Quantity, l.UnitAmount)).ToList();
        var issued = await issuer.IssueAsync(request.OrganisationId, request.ProgrammeId, request.CustomerName, lines, request.DueDays, currentUser.UserId ?? Guid.Empty, cancellationToken);
        if (issued.IsFailure)
        {
            return Result.Failure<InvoiceView>(issued.Error);
        }

        var invoice = issued.Value.Invoice;
        var views = issued.Value.Lines.Select(l => new InvoiceLineView(l.Line, l.Kind, l.Description, l.Quantity, l.UnitAmount, l.Amount)).ToList();
        return new InvoiceView(invoice.Id, invoice.Number, invoice.OrganisationId, invoice.ProgrammeId, invoice.Status.ToString().ToLowerInvariant(), invoice.Subtotal, invoice.Vat, invoice.Total, invoice.PaidAmount,
            invoice.IssuedAt, invoice.DueAt, invoice.PaidAt, invoice.VirtualAccountBank, invoice.VirtualAccountNumber, invoice.VirtualAccountName, views);
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
