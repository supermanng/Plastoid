using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Sponsorship.Features.Campaigns.GetProgrammeBudget;

/// <summary>Budget vs spend from the ledger (blueprint §9.12 programme ledger): invoice, funds received, fees earned, tutor earnings, refunds, balance held.</summary>
public sealed record GetProgrammeBudgetQuery(Guid OrganisationId, Guid ProgrammeId) : IQuery<BudgetView>;

public sealed record BudgetView(Guid ProgrammeId, Money Budget, Money QuoteTotal, Money Invoiced, Money Received, bool IsFunded, int SeatsUnlocked, int SeatsFilled, Money SeatFeesEarned, Money ManagementFeesEarned, Money TutorEarnings, Money CloseoutRefund, Money FundsHeld, IReadOnlyList<MonthView> Months);

public sealed record MonthView(int Month, int FilledSeats, Money ManagementFee, Money TutorEarnings, DateTime PostedAt);

internal sealed class GetProgrammeBudgetValidator : AbstractValidator<GetProgrammeBudgetQuery>
{
    public GetProgrammeBudgetValidator()
    {
        RuleFor(x => x.ProgrammeId).NotEmpty();
    }
}

internal sealed class GetProgrammeBudgetHandler(SponsorshipDbContext db, ILedgerService ledger, ICurrentUser currentUser) : IQueryHandler<GetProgrammeBudgetQuery, BudgetView>
{
    public async Task<Result<BudgetView>> Handle(GetProgrammeBudgetQuery request, CancellationToken cancellationToken)
    {
        var programme = await db.Programmes.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.ProgrammeId, cancellationToken);
        if (programme is null || (programme.OrganisationId != request.OrganisationId && !currentUser.Roles.Contains("staff")))
        {
            return Error.NotFound("programme_not_found", "No such programme for this organisation.");
        }

        var filled = await db.Seats.AsNoTracking().Where(s => s.ProgrammeId == programme.Id && s.Status == SeatStatus.Filled).ToListAsync(cancellationToken);
        var months = await db.Months.AsNoTracking().Where(m => m.ProgrammeId == programme.Id).OrderBy(m => m.MonthIndex).ToListAsync(cancellationToken);
        var seatFees = new Money(checked(FeeSchedule.SeatFee.AmountKobo * filled.Sum(s => s.FillCount)), FeeSchedule.SeatFee.Currency);
        var management = months.Aggregate(Money.Zero(), (acc, m) => acc + m.ManagementFee);
        var tutor = months.Aggregate(Money.Zero(), (acc, m) => acc + m.TutorEarnings);
        var held = await ledger.GetBalanceAsync(AccountCodes.ProgrammeFundsHeld, programme.Id, cancellationToken);
        return new BudgetView(programme.Id, programme.Budget, programme.QuoteTotal, programme.InvoiceId is null ? Money.Zero() : programme.QuoteTotal, programme.FundedAmount, programme.IsFunded, programme.SeatsUnlocked, filled.Count,
            seatFees, management, tutor, programme.CloseoutRefund, held, months.Select(m => new MonthView(m.MonthIndex, m.FilledSeats, m.ManagementFee, m.TutorEarnings, m.PostedAt)).ToList());
    }
}

internal static class GetProgrammeBudgetEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/{programmeId:guid}/budget", async (Guid organisationId, Guid programmeId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetProgrammeBudgetQuery(organisationId, programmeId), ct)).ToHttpResult())
            .WithName("GetProgrammeBudget")
            .WithSummary("Sponsor: budget vs spend read from the ledger's programme sub-account.")
            .RequireAuthorization(IdentityPolicies.OrganisationMember);
    }
}
