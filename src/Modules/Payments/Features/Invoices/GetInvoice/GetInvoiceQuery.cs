using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Payments.Features.Invoices.GetInvoice;

public sealed record GetInvoiceQuery(Guid OrganisationId, Guid InvoiceId) : IQuery<InvoiceDetail>;

public sealed record InvoiceDetailLine(int Line, string Kind, string Description, int Quantity, Money UnitAmount, Money Amount);

public sealed record InvoiceDetail(Guid InvoiceId, string Number, Guid OrganisationId, Guid? ProgrammeId, string Status, Money Subtotal, Money Vat, Money Total, Money PaidAmount, Money Outstanding, DateTime IssuedAt, DateTime DueAt, DateTime? PaidAt, string? VirtualAccountBank, string? VirtualAccountNumber, string? VirtualAccountName, IReadOnlyList<InvoiceDetailLine> Lines);

internal sealed class GetInvoiceHandler(PaymentsDbContext db) : IQueryHandler<GetInvoiceQuery, InvoiceDetail>
{
    public async Task<Result<InvoiceDetail>> Handle(GetInvoiceQuery request, CancellationToken cancellationToken)
    {
        var i = await db.Invoices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.InvoiceId && x.OrganisationId == request.OrganisationId, cancellationToken);
        if (i is null)
        {
            return Error.NotFound("invoice_not_found", "No such invoice.");
        }

        var lines = await db.InvoiceLines.AsNoTracking().Where(l => l.InvoiceId == i.Id).OrderBy(l => l.Line).ToListAsync(cancellationToken);
        return new InvoiceDetail(i.Id, i.Number, i.OrganisationId, i.ProgrammeId, i.Status.ToString().ToLowerInvariant(), i.Subtotal, i.Vat, i.Total, i.PaidAmount, i.Outstanding, i.IssuedAt, i.DueAt, i.PaidAt,
            i.VirtualAccountBank, i.VirtualAccountNumber, i.VirtualAccountName, lines.Select(l => new InvoiceDetailLine(l.Line, l.Kind, l.Description, l.Quantity, l.UnitAmount, l.Amount)).ToList());
    }
}

internal static class GetInvoiceEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/organisations/{organisationId:guid}/invoices/{invoiceId:guid}", async (Guid organisationId, Guid invoiceId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetInvoiceQuery(organisationId, invoiceId), ct)).ToHttpResult())
            .WithName("GetInvoice")
            .RequireAuthorization(IdentityPolicies.OrganisationMember);
    }
}
