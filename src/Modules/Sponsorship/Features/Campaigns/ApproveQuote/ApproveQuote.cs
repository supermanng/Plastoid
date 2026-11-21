using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Features.Campaigns.ApproveQuote;

/// <summary>Sponsor signs off the quote; the invoice (with its dedicated virtual account) is issued in the same transaction through Payments.Contracts.</summary>
public sealed record ApproveQuoteCommand(Guid OrganisationId, Guid ProgrammeId, int? DueDays, string IdempotencyKey) : ICommand<ApproveQuoteResponse>, IIdempotentCommand;

public sealed record ApproveQuoteRequest(int? DueDays);

public sealed record ApproveQuoteResponse(Guid ProgrammeId, string Status, Guid InvoiceId, string InvoiceNumber, Money Subtotal, Money Vat, Money Total, DateTime DueAt, string? VirtualAccountBank, string? VirtualAccountNumber, string? VirtualAccountName);

internal sealed class ApproveQuoteValidator : AbstractValidator<ApproveQuoteCommand>
{
    public ApproveQuoteValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
        RuleFor(x => x.ProgrammeId).NotEmpty();
        RuleFor(x => x.DueDays).InclusiveBetween(1, 90).When(x => x.DueDays is not null);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ApproveQuoteHandler(ProgrammeAccess access, ISponsorInvoicing invoicing, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<ApproveQuoteCommand, ApproveQuoteResponse>
{
    public async Task<Result<ApproveQuoteResponse>> Handle(ApproveQuoteCommand request, CancellationToken cancellationToken)
    {
        var loaded = await access.ForOrganisationAsync(request.OrganisationId, request.ProgrammeId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<ApproveQuoteResponse>(loaded.Error);
        }

        var programme = loaded.Value;
        var approved = programme.ApproveQuote(currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        if (approved.IsFailure)
        {
            return Result.Failure<ApproveQuoteResponse>(approved.Error);
        }

        var quote = programme.Quote;
        var lines = new List<SponsorInvoiceLine>
        {
            new(SponsorInvoiceLineKinds.Tuition, $"Tuition: {quote.Seats} seats × {programme.CourseTitle}", quote.Seats, quote.TuitionPerSeatSponsorShare),
            new(SponsorInvoiceLineKinds.ManagementFee, "Programme management fee", 1, quote.ManagementFee),
            new(SponsorInvoiceLineKinds.SeatFee, "Platform seat fee", quote.Seats, FeeSchedule.SeatFee),
        };
        var invoice = await invoicing.IssueAsync(new SponsorInvoiceRequest(programme.OrganisationId, programme.Id, programme.SponsorName, lines, request.DueDays ?? 14), cancellationToken);
        if (invoice.IsFailure)
        {
            return Result.Failure<ApproveQuoteResponse>(invoice.Error);
        }

        if (invoice.Value.Total != quote.Total)
        {
            return Error.Failure("invoice_total_mismatch", $"Invoice total {invoice.Value.Total} differs from the quote {quote.Total}.");
        }

        programme.AttachInvoice(invoice.Value.InvoiceId);
        outbox.Enqueue(NotificationRequested.To(programme.CreatedBy, "sponsorship.quote.approved", new Dictionary<string, string>
        {
            ["programmeId"] = programme.Id.ToString("D"),
            ["invoiceNumber"] = invoice.Value.Number,
            ["total"] = invoice.Value.Total.ToString(),
            ["virtualAccount"] = invoice.Value.VirtualAccountNumber ?? string.Empty,
        }));
        var i = invoice.Value;
        return new ApproveQuoteResponse(programme.Id, programme.Status.ToString().ToLowerInvariant(), i.InvoiceId, i.Number, i.Subtotal, i.Vat, i.Total, i.DueAt, i.VirtualAccountBank, i.VirtualAccountNumber, i.VirtualAccountName);
    }
}

internal static class ApproveQuoteEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{programmeId:guid}/quote/approve", async (Guid organisationId, Guid programmeId, ApproveQuoteRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ApproveQuoteCommand(organisationId, programmeId, body?.DueDays, key), ct)).ToHttpResult();
            })
            .WithName("ApproveQuote")
            .WithSummary("Sponsor owner/finance: approve the quote; the invoice and its virtual account are issued. Requires Idempotency-Key.")
            .RequireAuthorization(IdentityPolicies.OrganisationFinance);
    }
}
