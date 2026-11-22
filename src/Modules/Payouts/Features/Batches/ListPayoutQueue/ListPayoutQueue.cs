using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payouts.Domain;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.Modules.Payouts.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payouts.Features.Batches.ListPayoutQueue;

/// <summary>Staff view of who would be paid next and why others are skipped, plus pending batches.</summary>
public sealed record ListPayoutQueueQuery : IQuery<PayoutQueueView>;

/// <summary>A payee in the queue. PayeeType "tutor" | "organisation" with PayeeId; TutorUserId is the payee id for tutors (older clients) and null for organisations.</summary>
public sealed record QueuePayee(Guid? TutorUserId, Money Payable, Money Held, Money Pending, bool Eligible, string? SkipReason, string? AccountNumberMasked, string PayeeType = "tutor", Guid PayeeId = default);

public sealed record QueueBatch(Guid BatchId, string Reference, string Status, int Count, Money Total, DateTime CreatedAt);

/// <summary>Tutors lists every payee (tutors and organisations); the name is kept for existing clients.</summary>
public sealed record PayoutQueueView(bool PayoutsFrozen, Money MinimumPayout, IReadOnlyList<QueuePayee> Tutors, IReadOnlyList<QueueBatch> OpenBatches);

internal sealed class ListPayoutQueueValidator : AbstractValidator<ListPayoutQueueQuery>
{
}

internal sealed class ListPayoutQueueHandler(PayoutsDbContext db, PayableCalculator calculator, PayeeDestinations destinations, IPayoutControls controls, IOptions<PayoutOptions> options, IClock clock) : IQueryHandler<ListPayoutQueueQuery, PayoutQueueView>
{
    public async Task<Result<PayoutQueueView>> Handle(ListPayoutQueueQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var minimum = Money.NairaAmount(options.Value.MinimumPayoutNaira);
        var payees = await db.Earnings.AsNoTracking().Select(e => new PayeeKey(e.PayeeType, e.PayeeId)).Distinct().ToListAsync(cancellationToken);
        var rows = new List<QueuePayee>();
        foreach (var payee in payees)
        {
            var summary = await calculator.SummariseAsync(payee.PayeeType, payee.PayeeId, now, cancellationToken);
            if (summary.Payable.IsZero && summary.Held.IsZero && summary.Pending.IsZero)
            {
                continue;
            }

            var account = await destinations.GetAsync(payee.PayeeType, payee.PayeeId, cancellationToken);
            var reason = summary.Payable < minimum ? "below_minimum"
                : summary.DisputeHold ? "dispute_open"
                : account is null ? "no_payout_account"
                : !account.NameMatched ? "name_mismatch"
                : null;
            rows.Add(new QueuePayee(payee.PayeeType == PayeeTypes.Tutor ? payee.PayeeId : null, summary.Payable, summary.Held, summary.Pending, reason is null, reason, account?.AccountNumberMasked, payee.PayeeType, payee.PayeeId));
        }

        var batches = await db.Batches.AsNoTracking().Where(b => b.Status == BatchStatus.PendingApproval || b.Status == BatchStatus.Approved || b.Status == BatchStatus.Submitted)
            .OrderByDescending(b => b.CreatedAt).Take(20).Select(b => new QueueBatch(b.Id, b.Reference, b.Status.ToString().ToLowerInvariant(), b.Count, b.Total, b.CreatedAt)).ToListAsync(cancellationToken);
        return new PayoutQueueView(await controls.ArePayoutsFrozenAsync(cancellationToken), minimum, rows.OrderByDescending(r => r.Payable.AmountKobo).ToList(), batches);
    }
}

internal static class ListPayoutQueueEndpoint
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapGet("/queue", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListPayoutQueueQuery(), ct)).ToHttpResult())
            .WithName("ListPayoutQueue")
            .WithSummary("Staff: tutors and organisations payable in the next run (with skip reasons) and open batches.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
