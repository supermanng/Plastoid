using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.Modules.Wallet.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Sponsorship.Features.Campaigns.FundFromWallet;

/// <summary>
/// Pay the programme invoice from the organisation's unit wallet (ADR 0018): whole units at today's rate cover what is
/// outstanding, the wallet releases them first-in, first-out, Payments settles the invoice (entry 6, units variant) and the
/// usual invoice-paid event funds the programme.
/// </summary>
public sealed record FundFromWalletCommand(Guid OrganisationId, Guid ProgrammeId, string IdempotencyKey) : ICommand<FundFromWalletResponse>, IIdempotentCommand;

public sealed record FundFromWalletResponse(Guid ProgrammeId, Guid InvoiceId, string InvoiceStatus, long UnitsRedeemed, long UnitRateKobo, Money AmountApplied, Money Outstanding, long WalletBalanceAfter);

internal sealed class FundFromWalletValidator : AbstractValidator<FundFromWalletCommand>
{
    public FundFromWalletValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
        RuleFor(x => x.ProgrammeId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class FundFromWalletHandler(ProgrammeAccess access, ISponsorInvoicing invoicing, IUnitRates rates, IUnitWallet wallets) : ICommandHandler<FundFromWalletCommand, FundFromWalletResponse>
{
    public async Task<Result<FundFromWalletResponse>> Handle(FundFromWalletCommand request, CancellationToken cancellationToken)
    {
        var loaded = await access.ForOrganisationAsync(request.OrganisationId, request.ProgrammeId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<FundFromWalletResponse>(loaded.Error);
        }

        var programme = loaded.Value;
        if (programme.Status != ProgrammeStatus.QuoteApproved || programme.InvoiceId is not { } invoiceId)
        {
            return Error.Conflict("programme_not_invoiced", "Approve the quote first; the invoice is paid from the wallet.");
        }

        var invoice = await invoicing.GetAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return Error.NotFound("invoice_not_found", "The programme's invoice could not be found.");
        }

        if (invoice.Outstanding.IsZero || invoice.Outstanding.IsNegative)
        {
            return Error.Conflict("invoice_already_paid", "The invoice is already settled.");
        }

        var rate = await rates.CurrentAsync(cancellationToken);
        var units = rate.UnitsFor(invoice.Outstanding);
        var redeemed = await wallets.RedeemAsync(new UnitRedemptionRequest(WalletOwnerTypes.Organisation, programme.OrganisationId, units, $"invoice:{invoiceId:N}:units", "sponsorship", programme.Id, $"Programme funding: {programme.Name}"), cancellationToken);
        if (redeemed.IsFailure)
        {
            return Result.Failure<FundFromWalletResponse>(redeemed.Error);
        }

        var settled = await invoicing.SettleFromUnitsAsync(new SettleInvoiceFromUnitsRequest(invoiceId, invoice.Outstanding, redeemed.Value.Value, redeemed.Value.WalletId, $"units:{redeemed.Value.TransactionId:N}"), cancellationToken);
        if (settled.IsFailure)
        {
            return Result.Failure<FundFromWalletResponse>(settled.Error);
        }

        return new FundFromWalletResponse(programme.Id, invoiceId, settled.Value.Status, units, rate.RateKobo, invoice.Outstanding, settled.Value.Outstanding, redeemed.Value.BalanceAfter);
    }
}

internal static class FundFromWalletEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{programmeId:guid}/fund-from-wallet", async (Guid organisationId, Guid programmeId, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new FundFromWalletCommand(organisationId, programmeId, key), ct)).ToHttpResult();
            })
            .WithName("FundProgrammeFromWallet")
            .WithSummary("Sponsor owner/finance: pay the programme invoice with units from the organisation's wallet; the programme is funded at once. Requires Idempotency-Key.")
            .RequireAuthorization(IdentityPolicies.OrganisationFinance);
    }
}
