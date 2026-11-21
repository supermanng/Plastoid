using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Behaviours;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Jobs;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Messaging.Outbox;
using Plastoids.Infrastructure.Modules;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Persistence.Interceptors;
using Plastoids.Infrastructure.Persistence.Shared;
using Plastoids.Infrastructure.Secrets;
using Plastoids.Infrastructure.Security;
using Plastoids.Infrastructure.Sms;
using Plastoids.Infrastructure.Storage;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Infrastructure.Time;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;
using Quartz;
using StackExchange.Redis;

namespace Plastoids.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "Plastoids";

    /// <summary>Cross-cutting services: clock, tenancy, MySQL connection, Kafka, Redis, storage, Quartz, pipeline behaviours.</summary>
    public static IServiceCollection AddPlastoidsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddScoped<CurrentContext>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentContext>());
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentContext>());

        // MySQL: one connection per scope, shared by every module DbContext.
        services.Configure<MySqlOptions>(configuration.GetSection(MySqlOptions.SectionName));
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is not configured.");
        services.AddScoped(_ => new ScopedDbConnection(EnsureGuidFormat(connectionString)));
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<TenantWriteGuardInterceptor>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOutbox, OutboxAccumulator>();
        services.AddScoped<EnvelopeSerializer>();
        services.AddDbContext<SharedDbContext>((sp, o) => o.UsePlastoidsMySql(sp));

        // Kafka
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddSingleton<TopicRegistry>();
        services.AddSingleton<ConsumerRegistry>();
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        services.AddHostedService<TopicProvisioner>();

        // Redis
        var redis = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));

        // Storage / secrets
        services.Configure<BlobStorageOptions>(configuration.GetSection(BlobStorageOptions.SectionName));
        services.AddSingleton<IBlobStorage, S3BlobStorage>();
        services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();

        // Outbound integrations (blueprint §17): fakes unless Integrations:<Name> selects a real provider.
        AddIntegration<IKycProvider, FakeKycProvider, SmileIdKycProvider>(services, configuration, "Kyc", "smileid");
        AddIntegration<IBankAccountResolver, FakeBankAccountResolver, PaystackBankAccountResolver>(services, configuration, "BankResolver", "paystack");
        AddIntegration<ICalendarProvider, FakeCalendarProvider, GoogleCalendarProvider>(services, configuration, "Calendar", "google");
        AddIntegration<IVideoProvider, FakeVideoProvider, MuxVideoProvider>(services, configuration, "Video", "mux");
        AddIntegration<IGatewaySettlementReport, FakeGatewaySettlementReport, FakeGatewaySettlementReport>(services, configuration, "SettlementReport", "none");
        AddIntegration<IBankStatement, FakeBankStatement, FakeBankStatement>(services, configuration, "BankStatement", "none");
        var gateway = configuration["Integrations:PaymentGateway"] ?? "fake";
        switch (gateway.ToLowerInvariant())
        {
            case "paystack":
                services.AddHttpClient<IPaymentGateway, PaystackGateway>();
                break;
            case "flutterwave":
                services.AddSingleton<IPaymentGateway, FlutterwaveGateway>();
                break;
            default:
                services.AddSingleton<FakePaymentGateway>();
                services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<FakePaymentGateway>());
                break;
        }

        // Security primitives (column encryption, password hashing) and outbound SMS.
        services.AddSingleton<IFieldEncryptor, AesGcmFieldEncryptor>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.Configure<SmsOptions>(configuration.GetSection(SmsOptions.SectionName));
        services.Configure<Http.AdminOptions>(configuration.GetSection(Http.AdminOptions.SectionName));
        if (string.Equals(configuration[$"{SmsOptions.SectionName}:Provider"], "termii", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<ISmsSender, TermiiSmsSender>();
        }
        else
        {
            services.AddSingleton<FakeSmsSender>();
            services.AddSingleton<ISmsSender>(sp => sp.GetRequiredService<FakeSmsSender>());
        }

        // Quartz (RAM store in Sprint 0; MySQL clustered store is ADR-0004's follow-up).
        services.AddQuartz(q =>
        {
            q.SchedulerName = "plastoids";
            q.UseSimpleTypeLoader();
            q.UseInMemoryStore();
            q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 8);
        });
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        // Modules
        services.AddSingleton<ModuleRegistry>();
        if (configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            // First hosted service registered → runs before Quartz, Kafka consumers and the server (dev/test only).
            services.AddHostedService<MigrateOnStartupService>();
        }

        services.AddValidatorsFromAssembly(typeof(InfrastructureServiceCollectionExtensions).Assembly, includeInternalTypes: true);

        return services;
    }

    private static void AddIntegration<TInterface, TFake, TReal>(IServiceCollection services, IConfiguration configuration, string name, string realKey)
        where TInterface : class
        where TFake : class, TInterface
        where TReal : class, TInterface
    {
        if (string.Equals(configuration[$"Integrations:{name}"], realKey, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<TInterface, TReal>();
        }
        else
        {
            services.AddSingleton<TFake>();
            services.AddSingleton<TInterface>(sp => sp.GetRequiredService<TFake>());
        }
    }

    /// <summary>Registers a module: services, DbContext, handlers, validators, outbox job, consumer host, topics.</summary>
    public static IServiceCollection AddModule<TModule>(this IServiceCollection services, IConfiguration configuration)
        where TModule : class, IModule, new()
    {
        var module = new TModule();
        services.AddSingleton<IModule>(module);

        // MediatR handlers + validators from the module assembly
        services.AddMediatR(c => c.RegisterServicesFromAssembly(module.Assembly));
        services.AddValidatorsFromAssembly(module.Assembly, includeInternalTypes: true);

        // Module DbContext on the shared scoped connection; also resolvable as ModuleDbContext by type.
        var addDbContext = typeof(InfrastructureServiceCollectionExtensions)
            .GetMethod(nameof(AddModuleDbContext), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(module.DbContextType);
        addDbContext.Invoke(null, [services, module.Name]);

        // Consumers: each handler type scoped; host per module.
        foreach (var registration in module.Consumers())
        {
            services.AddScoped(registration.HandlerType);
        }

        services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(sp => new KafkaConsumerHost(
            module.Name,
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ConsumerRegistry>(),
            sp.GetRequiredService<IKafkaProducer>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<IOptions<KafkaOptions>>(),
            sp.GetRequiredService<ILogger<KafkaConsumerHost>>()));

        module.RegisterServices(services, configuration);
        return services;
    }

    private static void AddModuleDbContext<TContext>(IServiceCollection services, string moduleName)
        where TContext : ModuleDbContext
    {
        services.AddDbContext<TContext>((sp, o) => o.UsePlastoidsMySql(sp));

        services.AddQuartz(q =>
        {
            var publishKey = new JobKey($"outbox-publish-{moduleName}");
            q.AddJob<OutboxPublisherJob<TContext>>(j => j.WithIdentity(publishKey).StoreDurably());
            q.AddTrigger(t => t.ForJob(publishKey).WithIdentity($"{publishKey.Name}-trigger")
                .StartNow().WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMilliseconds(500)).RepeatForever()));

            var purgeKey = new JobKey($"outbox-purge-{moduleName}");
            q.AddJob<OutboxPurgeJob<TContext>>(j => j.WithIdentity(purgeKey).StoreDurably());
            q.AddTrigger(t => t.ForJob(purgeKey).WithIdentity($"{purgeKey.Name}-trigger")
                .WithCronSchedule("0 30 2 * * ?")); // 02:30 UTC daily
        });
    }

    /// <summary>Pipeline order (outer → inner): Logging → Validation → UnitOfWork → Idempotency → Audit → handler.</summary>
    public static IServiceCollection AddPlastoidsPipeline(this IServiceCollection services)
    {
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehaviour<,>));
        return services;
    }

    /// <summary>GuidFormat=Binary16 makes MySqlConnector store Guids big-endian in BINARY(16), preserving UUID v7 ordering.</summary>
    public static string EnsureGuidFormat(string connectionString)
    {
        var builder = new MySqlConnector.MySqlConnectionStringBuilder(connectionString)
        {
            GuidFormat = MySqlConnector.MySqlGuidFormat.Binary16,
        };
        if (builder.ConnectionTimeout == 15)
        {
            builder.ConnectionTimeout = 10;
        }

        return builder.ConnectionString;
    }

    private sealed class MigrateOnStartupService(IServiceProvider provider) : Microsoft.Extensions.Hosting.IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Migrator.MigrateAllAsync(provider, cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
