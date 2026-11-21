using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.Modules.Payments.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payments.Features.Settlement.SweepEarnedRevenue;

/// <summary>
/// Blueprint entry 3: commission earned on settled payments past the 7-day refund window, less gateway fees, moves from
/// client funds (1020) to operating (1010). One journal entry per run.
/// </summary>
public sealed record SweepEarnedRevenueCommand : ICommand<SweepEarnedRevenueResponse>;

public sealed record SweepEarnedRevenueResponse(int Payments, Money Amount, Guid? LedgerEntryId);

internal sealed class SweepEarnedRevenueValidator : AbstractValidator<SweepEarnedRevenueCommand>
{
}

internal sealed class SweepEarnedRevenueHandler(PaymentsDbContext db, ILedgerService ledger, Microsoft.Extensions.Options.IOptions<PaymentOptions> options, IClock clock) : ICommandHandler<SweepEarnedRevenueCommand, SweepEarnedRevenueResponse>
{
    public async Task<Result<SweepEarnedRevenueResponse>> Handle(SweepEarnedRevenueCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var cutoff = now - TimeSpan.FromDays(options.Value.SweepHoldDays);
        var eligible = await db.Intents
            .Where(i => i.Status == PaymentStatus.Succeeded && i.SettledAt != null && i.SweptAt == null && i.SucceededAt <= cutoff)
            .ToListAsync(cancellationToken);
        eligible = eligible.Where(i => i.RefundedAmount.IsZero).ToList();
        if (eligible.Count == 0)
        {
            return new SweepEarnedRevenueResponse(0, Money.Zero(), null);
        }

        var total = eligible.Aggregate(Money.Zero(), (sum, i) => sum + i.SweepableCommission);
        if (total.IsZero || total.IsNegative)
        {
            foreach (var i in eligible)
            {
                i.MarkSwept(now);
            }

            return new SweepEarnedRevenueResponse(eligible.Count, Money.Zero(), null);
        }

        var batch = $"{now:yyyyMMddHHmmss}-{eligible.Count}";
        var entry = await ledger.PostAsync(PaymentPostings.Entry3Sweep(batch, total, now), cancellationToken);
        if (entry.IsFailure)
        {
            return Result.Failure<SweepEarnedRevenueResponse>(entry.Error);
        }

        foreach (var i in eligible)
        {
            i.MarkSwept(now);
            db.Transactions.Add(PaymentTransaction.Record(i.TenantId, i.Id, TransactionKinds.Sweep, i.SweepableCommission, Money.Zero(), i.GatewayReference, now, entry.Value));
        }

        return new SweepEarnedRevenueResponse(eligible.Count, total, entry.Value);
    }
}

internal static class SweepEarnedRevenueEndpoint
{
    public static void Map(IEndpointRouteBuilder adminGroup)
    {
        adminGroup.MapPost("/sweeps/run", async (ISender sender, CancellationToken ct) => (await sender.Send(new SweepEarnedRevenueCommand(), ct)).ToHttpResult())
            .WithName("SweepEarnedRevenue")
            .WithSummary("Staff: sweep earned commission to the operating account (entry 3). Also runs weekly.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
