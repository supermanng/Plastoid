using MediatR;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Payments.Features.Settlement.ImportSettlements;
using Plastoids.Modules.Payments.Features.Settlement.SweepEarnedRevenue;
using Plastoids.SharedKernel.Tenancy;
using Quartz;

namespace Plastoids.Modules.Payments.Jobs;

/// <summary>Daily 04:00 UTC: import the gateway settlement report (entry 2).</summary>
[DisallowConcurrentExecution]
public sealed class SettlementImportJob(ISender sender) : IJob
{
    public const string JobName = "payments-settlement-import";

    public async Task Execute(IJobExecutionContext context)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await sender.Send(new ImportSettlementsCommand(null), context.CancellationToken);
    }
}

/// <summary>Weekly (Mondays 05:00 UTC): sweep earned commission to operating (entry 3).</summary>
[DisallowConcurrentExecution]
public sealed class RevenueSweepJob(ISender sender) : IJob
{
    public const string JobName = "payments-revenue-sweep";

    public async Task Execute(IJobExecutionContext context)
    {
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await sender.Send(new SweepEarnedRevenueCommand(), context.CancellationToken);
    }
}
