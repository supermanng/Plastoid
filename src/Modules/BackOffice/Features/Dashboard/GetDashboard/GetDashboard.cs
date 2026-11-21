using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.BackOffice.Domain;
using Plastoids.Modules.BackOffice.Persistence;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.BackOffice.Features.Dashboard.GetDashboard;

/// <summary>Executive dashboard (blueprint §9.20): ledger balances + event-derived counters; nothing is recomputed on screen.</summary>
public sealed record GetDashboardQuery : IQuery<DashboardView>;

public sealed record MetricView(string Key, long Count, Money Amount, DateTime? LastEventAt);

public sealed record RevenueView(Money Gmv, Money Refunds, Money CourseCommission, Money TutoringCommission, Money ProgrammeManagementFees, Money PlatformSeatFees, Money ProcessingFees, Money NetRevenue);

public sealed record LiabilitiesView(Money ClientFunds, Money GatewayInTransit, Money TutorPayable, Money ProgrammeFundsHeld, Money LearnerRefundCredit, Money VatPayable);

public sealed record ParticipantsView(long LearnersRegistered, long TutorsVerified, long SponsorsVerified);

public sealed record ProgrammeHealthView(long Funded, long Published, long EnrolmentsCreated, long EnrolmentsCompleted, decimal CompletionRatePercent);

public sealed record PayoutQueueView(long BatchesApproved, Money ApprovedTotal, long Settled, Money SettledTotal, long Failed, Money FailedTotal, bool PayoutsFrozen);

public sealed record RiskAlertView(Guid AlertId, string Kind, string Severity, string Message, Guid? ReferenceId, DateTime RaisedAt);

public sealed record DashboardView(DateTime GeneratedAt, RevenueView Revenue, LiabilitiesView Liabilities, ParticipantsView Participants, ProgrammeHealthView Programmes, PayoutQueueView Payouts, IReadOnlyList<RiskAlertView> RiskAlerts, IReadOnlyList<MetricView> Metrics);

internal sealed class GetDashboardValidator : AbstractValidator<GetDashboardQuery>
{
}

internal sealed class GetDashboardHandler(BackOfficeDbContext db, ILedgerService ledger, IPayoutControls payouts, IClock clock) : IQueryHandler<GetDashboardQuery, DashboardView>
{
    public async Task<Result<DashboardView>> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        var metrics = await db.Metrics.AsNoTracking().ToDictionaryAsync(m => m.Key, ct);
        long Count(string key) => metrics.TryGetValue(key, out var m) ? m.Count : 0;
        Money Amount(string key) => metrics.TryGetValue(key, out var m) ? m.Amount : Money.Zero();
        async Task<Money> Bal(string code) => await ledger.GetBalanceAsync(code, null, ct);

        var course = await Bal(AccountCodes.CourseCommission);
        var tutoring = await Bal(AccountCodes.TutoringCommission);
        var management = await Bal(AccountCodes.ProgrammeManagementFee);
        var seatFees = await Bal(AccountCodes.PlatformSeatFee);
        var processing = await Bal(AccountCodes.ProcessingFees);
        var revenue = new RevenueView(Amount(Metrics.Gmv), Amount(Metrics.Refunds), course, tutoring, management, seatFees, processing, course + tutoring + management + seatFees - processing);
        var liabilities = new LiabilitiesView(await Bal(AccountCodes.BankClientFunds), await Bal(AccountCodes.GatewayReceivable), await Bal(AccountCodes.TutorPayable), await Bal(AccountCodes.ProgrammeFundsHeld), await Bal(AccountCodes.LearnerRefundCredit), await Bal(AccountCodes.VatPayable));
        var participants = new ParticipantsView(Count(Metrics.LearnersRegistered), Count(Metrics.TutorsVerified), Count(Metrics.SponsorsVerified));
        var created = Count(Metrics.EnrolmentsCreated);
        var completed = Count(Metrics.EnrolmentsCompleted);
        var programmes = new ProgrammeHealthView(Count(Metrics.ProgrammesFunded), Count(Metrics.ProgrammesPublished), created, completed, created == 0 ? 0 : Math.Round(100m * completed / created, 1));
        var frozen = await payouts.ArePayoutsFrozenAsync(ct);
        var payoutQueue = new PayoutQueueView(Count(Metrics.PayoutBatchesApproved), Amount(Metrics.PayoutBatchesApproved), Count(Metrics.PayoutsSettled), Amount(Metrics.PayoutsSettled), Count(Metrics.PayoutsFailed), Amount(Metrics.PayoutsFailed), frozen);
        var alerts = await db.Alerts.AsNoTracking().Where(a => a.ResolvedAt == null).OrderByDescending(a => a.RaisedAt).Take(50)
            .Select(a => new RiskAlertView(a.Id, a.Kind, a.Severity, a.Message, a.ReferenceId, a.RaisedAt)).ToListAsync(ct);
        if (frozen && !alerts.Any(a => a.Kind == AlertKinds.Shortfall))
        {
            alerts.Insert(0, new RiskAlertView(Guid.Empty, AlertKinds.Shortfall, "critical", "Payouts are frozen by the reconciliation control", null, clock.UtcNow));
        }

        return new DashboardView(clock.UtcNow, revenue, liabilities, participants, programmes, payoutQueue, alerts,
            metrics.Values.OrderBy(m => m.Key).Select(m => new MetricView(m.Key, m.Count, m.Amount, m.LastEventAt)).ToList());
    }
}

internal static class GetDashboardEndpoint
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapGet("/dashboard", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetDashboardQuery(), ct)).ToHttpResult())
            .WithName("GetExecutiveDashboard")
            .WithSummary("Staff: GMV, net revenue, participants, programme health, payout queue and risk alerts from the ledger and event projections.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
