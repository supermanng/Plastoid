using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.Modules.Payouts.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payouts.Features.Organisations.GetOrganisationEarnings;

/// <summary>ADR 0019: the organisation's statement (same shape as /payouts/me) plus its payout account.</summary>
public sealed record GetOrganisationEarningsQuery(Guid OrganisationId) : IQuery<OrganisationEarningsView>;

public sealed record PayoutAccountSummary(string BankCode, string AccountNumberMasked, string AccountName, bool NameMatched);

public sealed record OrganisationEarningsView(Money Gross, Money Commission, Money Net, Money Held, Money Available, Money Pending, Money Paid, bool DisputeHold, IReadOnlyList<PayoutView> RecentPayouts, PayoutAccountSummary? PayoutAccount);

internal sealed class GetOrganisationEarningsValidator : AbstractValidator<GetOrganisationEarningsQuery>
{
    public GetOrganisationEarningsValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
    }
}

internal sealed class GetOrganisationEarningsHandler(PayoutsDbContext db, EarningsStatements statements, IClock clock) : IQueryHandler<GetOrganisationEarningsQuery, OrganisationEarningsView>
{
    public async Task<Result<OrganisationEarningsView>> Handle(GetOrganisationEarningsQuery request, CancellationToken cancellationToken)
    {
        var s = await statements.BuildAsync(PayeeTypes.Organisation, request.OrganisationId, clock.UtcNow, cancellationToken);
        var account = await db.OrganisationAccounts.AsNoTracking().Where(a => a.OrganisationId == request.OrganisationId)
            .Select(a => new PayoutAccountSummary(a.BankCode, a.AccountNumberMasked, a.AccountName, a.NameMatched)).SingleOrDefaultAsync(cancellationToken);
        return new OrganisationEarningsView(s.Gross, s.Commission, s.Net, s.Held, s.Available, s.Pending, s.Paid, s.DisputeHold, s.RecentPayouts, account);
    }
}

internal static class GetOrganisationEarningsEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/earnings", async (Guid organisationId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetOrganisationEarningsQuery(organisationId), ct)).ToHttpResult())
            .WithName("GetOrganisationEarnings")
            .WithSummary("Organisation member: gross, commission, net, held, available, pending and paid for course sales, with recent payouts and the payout account.")
            .RequireAuthorization(IdentityPolicies.OrganisationMember);
    }
}
