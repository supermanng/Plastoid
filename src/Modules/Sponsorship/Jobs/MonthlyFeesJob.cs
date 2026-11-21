using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;
using Quartz;

namespace Plastoids.Modules.Sponsorship.Jobs;

/// <summary>
/// RecordMonthlyFees (build prompt Sprint 6): for every completed programme month, entry 8 (management fee on filled seats)
/// and entry 9 (tutor earnings for the month) are posted once. Runs nightly; a month is "completed" once 30.44 days have elapsed.
/// </summary>
[DisallowConcurrentExecution]
public sealed class MonthlyFeesJob(SponsorshipDbContext db, IUnitOfWork unitOfWork, ILedgerService ledger, IOutbox outbox, IClock clock, ILogger<MonthlyFeesJob> logger) : IJob
{
    public const string JobName = "sponsorship-monthly-fees";
    private const double DaysPerMonth = 30.4375;

    public async Task Execute(IJobExecutionContext context) => await RunAsync(clock.UtcNow, context.CancellationToken);

    public async Task<int> RunAsync(DateTime now, CancellationToken ct)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await unitOfWork.BeginAsync(db, ct);
        var programmes = await db.Programmes.Where(p => p.Status == ProgrammeStatus.Published && p.StartsAt != null && p.StartsAt <= now).ToListAsync(ct);
        var posted = 0;
        foreach (var programme in programmes)
        {
            var elapsed = (int)Math.Floor((now - programme.StartsAt!.Value).TotalDays / DaysPerMonth);
            var monthsDue = Math.Min(programme.DurationMonths, elapsed);
            if (monthsDue <= 0)
            {
                continue;
            }

            var recorded = await db.Months.Where(m => m.ProgrammeId == programme.Id).Select(m => m.MonthIndex).ToListAsync(ct);
            var filled = await db.Seats.CountAsync(s => s.ProgrammeId == programme.Id && s.Status == SeatStatus.Filled, ct);
            for (var month = 1; month <= monthsDue; month++)
            {
                if (recorded.Contains(month))
                {
                    continue;
                }

                var (management, tuition) = SponsorshipPostings.MonthlyAmounts(programme, filled);
                if (!management.IsZero)
                {
                    Throw(await ledger.PostAsync(SponsorshipPostings.Entry8ManagementFee(programme, month, management, now), ct));
                }

                if (!tuition.IsZero && programme.TutorUserId is not null)
                {
                    Throw(await ledger.PostAsync(SponsorshipPostings.Entry9TutorEarnings(programme, month, tuition, now), ct));
                    outbox.Enqueue(new TutorEarningsAccrued(programme.Id, programme.Id, programme.TutorUserId.Value, month, tuition));
                }

                db.Months.Add(ProgrammeMonth.Record(programme.TenantId, programme.Id, month, filled, management, tuition, now));
                posted++;
            }
        }

        await unitOfWork.CommitAsync(ct);
        if (posted > 0)
        {
            logger.LogInformation("Sponsorship monthly fees: {Months} programme-months posted", posted);
        }

        return posted;
    }

    private static void Throw(SharedKernel.Results.Result<Guid> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Ledger post failed: {result.Error.Code} {result.Error.Message}");
        }
    }
}
