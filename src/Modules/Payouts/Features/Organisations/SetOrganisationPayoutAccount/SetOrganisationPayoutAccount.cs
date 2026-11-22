using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Payouts.Domain;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payouts.Features.Organisations.SetOrganisationPayoutAccount;

/// <summary>ADR 0019: an owner or finance member records where the organisation is paid. The bank account name must match the registered organisation name.</summary>
public sealed record SetOrganisationPayoutAccountCommand(Guid OrganisationId, string BankCode, string AccountNumber) : ICommand<OrganisationPayoutAccountView>;

public sealed record SetOrganisationPayoutAccountRequest(string BankCode, string AccountNumber);

public sealed record OrganisationPayoutAccountView(string BankCode, string AccountNumberMasked, string AccountName, bool NameMatched);

internal sealed class SetOrganisationPayoutAccountValidator : AbstractValidator<SetOrganisationPayoutAccountCommand>
{
    public SetOrganisationPayoutAccountValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
        RuleFor(x => x.BankCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.AccountNumber).NotEmpty().Length(10).Matches("^[0-9]+$").WithMessage("NUBAN account numbers are 10 digits.");
    }
}

internal sealed class SetOrganisationPayoutAccountHandler(PayoutsDbContext db, IOrganisationDirectory organisations, IBankAccountResolver resolver, IFieldEncryptor encryptor, ICurrentTenant tenant, IClock clock)
    : ICommandHandler<SetOrganisationPayoutAccountCommand, OrganisationPayoutAccountView>
{
    public async Task<Result<OrganisationPayoutAccountView>> Handle(SetOrganisationPayoutAccountCommand request, CancellationToken cancellationToken)
    {
        var organisation = await organisations.GetAsync(request.OrganisationId, cancellationToken);
        if (organisation is null)
        {
            return Error.NotFound("organisation_not_found", "No such organisation.");
        }

        if (!organisation.IsVerified)
        {
            return Error.Validation("organisation_not_verified", "The organisation must be verified before it can be paid.");
        }

        var resolved = await resolver.ResolveAsync(request.BankCode, request.AccountNumber, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<OrganisationPayoutAccountView>(resolved.Error);
        }

        if (!OrganisationPayoutAccount.NamesMatch(organisation.Name, resolved.Value.AccountName))
        {
            return Error.Validation("payout_name_mismatch", "The bank account name does not match the registered organisation name.");
        }

        var masked = "******" + request.AccountNumber[^4..];
        var cipher = encryptor.Encrypt(request.AccountNumber);
        var now = clock.UtcNow;
        var existing = await db.OrganisationAccounts.SingleOrDefaultAsync(a => a.OrganisationId == request.OrganisationId, cancellationToken);
        if (existing is null)
        {
            db.OrganisationAccounts.Add(OrganisationPayoutAccount.Create(tenant.TenantId, request.OrganisationId, request.BankCode, cipher, masked, resolved.Value.AccountName, true, now));
        }
        else
        {
            existing.Replace(request.BankCode, cipher, masked, resolved.Value.AccountName, true, now);
        }

        return new OrganisationPayoutAccountView(request.BankCode, masked, resolved.Value.AccountName, true);
    }
}

internal static class SetOrganisationPayoutAccountEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPut("/payout-account", async (Guid organisationId, SetOrganisationPayoutAccountRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SetOrganisationPayoutAccountCommand(organisationId, body.BankCode, body.AccountNumber), ct)).ToHttpResult())
            .WithName("SetOrganisationPayoutAccount")
            .WithSummary("Organisation owner/finance: record the bank account the organisation is paid to; the account name must match the registered name.")
            .RequireAuthorization(IdentityPolicies.OrganisationFinance);
    }
}
