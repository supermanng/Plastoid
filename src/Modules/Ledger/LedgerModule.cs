using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Ledger.Features.Admin.GetBalance;
using Plastoids.Modules.Ledger.Features.Admin.GetReconciliationStatus;
using Plastoids.Modules.Ledger.Features.Admin.ListJournalEntries;
using Plastoids.Modules.Ledger.Features.Admin.UnfreezePayouts;
using Plastoids.Modules.Ledger.Jobs;
using Plastoids.Modules.Ledger.Persistence;
using Plastoids.Modules.Ledger.Services;
using Quartz;

namespace Plastoids.Modules.Ledger;

public sealed class LedgerModule : IModule
{
    public string Name => "ledger";
    public Assembly Assembly => typeof(LedgerModule).Assembly;
    public Type DbContextType => typeof(LedgerDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(ShortfallDetected)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(IntegrityAlert)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<LedgerService>();
        services.AddScoped<ILedgerService>(sp => sp.GetRequiredService<LedgerService>());
        services.AddScoped<IPayoutControls>(sp => sp.GetRequiredService<LedgerService>());
        services.AddScoped<ReconciliationJob>();
        services.AddQuartz(q =>
        {
            var verify = new JobKey(BalanceVerificationJob.JobName);
            q.AddJob<BalanceVerificationJob>(j => j.WithIdentity(verify).StoreDurably());
            q.AddTrigger(t => t.ForJob(verify).WithIdentity(verify.Name + "-trigger").WithCronSchedule("0 0 1 * * ?")); // 01:00 UTC nightly
            var reconcile = new JobKey(ReconciliationJob.JobName);
            q.AddJob<ReconciliationJob>(j => j.WithIdentity(reconcile).StoreDurably());
            q.AddTrigger(t => t.ForJob(reconcile).WithIdentity(reconcile.Name + "-trigger").WithCronSchedule("0 0 3 * * ?")); // 03:00 UTC daily
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/ledger").WithTags("Ledger (staff)");
        GetBalanceEndpoint.Map(admin);
        ListJournalEntriesEndpoint.Map(admin);
        GetReconciliationStatusEndpoint.Map(admin);
        UnfreezePayoutsEndpoint.Map(admin);
    }

    public IEnumerable<ConsumerRegistration> Consumers() => [];
}
