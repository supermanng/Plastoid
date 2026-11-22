using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Payouts.Services;

public sealed record PayoutView(Guid PayoutId, string Status, Money Amount, string AccountNumberMasked, string? BankReference, DateTime CreatedAt, DateTime? SettledAt, string? FailureReason);

/// <summary>Story G3 statement shape, shared by the tutor (/payouts/me) and organisation (/payouts/organisations/{id}/earnings) views.</summary>
public sealed record EarningsStatementView(Money Gross, Money Commission, Money Net, Money Held, Money Available, Money Pending, Money Paid, bool DisputeHold, IReadOnlyList<PayoutView> RecentPayouts);

/// <summary>Builds the statement for one payee: the payable summary plus the 20 most recent payouts.</summary>
public sealed class EarningsStatements(PayoutsDbContext db, PayableCalculator calculator)
{
    public async Task<EarningsStatementView> BuildAsync(string payeeType, Guid payeeId, DateTime now, CancellationToken ct)
    {
        var summary = await calculator.SummariseAsync(payeeType, payeeId, now, ct);
        var recent = await db.Payouts.AsNoTracking().Where(p => p.PayeeType == payeeType && p.PayeeId == payeeId).OrderByDescending(p => p.CreatedAt).Take(20).ToListAsync(ct);
        return new EarningsStatementView(summary.Gross, summary.Commission, summary.Net, summary.Held, summary.Payable, summary.Pending, summary.Paid, summary.DisputeHold,
            recent.Select(p => new PayoutView(p.Id, p.Status.ToString().ToLowerInvariant(), p.Amount, p.AccountNumberMasked, p.BankReference, p.CreatedAt, p.SettledAt, p.FailureReason)).ToList());
    }
}
