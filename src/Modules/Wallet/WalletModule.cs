using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Wallet.Consumers;
using Plastoids.Modules.Wallet.Contracts;
using Plastoids.Modules.Wallet.Features.Admin.AdjustWallet;
using Plastoids.Modules.Wallet.Features.Admin.UnitRates;
using Plastoids.Modules.Wallet.Features.Wallets.GetWallet;
using Plastoids.Modules.Wallet.Features.Wallets.PurchaseUnits;
using Plastoids.Modules.Wallet.Persistence;
using Plastoids.Modules.Wallet.Services;

namespace Plastoids.Modules.Wallet;

/// <summary>Units (ADR 0018): prepaid, closed-loop balances for people and organisations, priced by an admin-approved rate.</summary>
public sealed class WalletModule : IModule
{
    public string Name => "wallet";
    public Assembly Assembly => typeof(WalletModule).Assembly;
    public Type DbContextType => typeof(WalletDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(UnitsPurchased)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(UnitsRedeemed)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WalletOptions>(configuration.GetSection(WalletOptions.SectionName));
        services.AddScoped<IUnitRates, UnitRates>();
        services.AddScoped<IUnitWallet, UnitWallet>();
        services.AddScoped<WalletAccess>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var wallet = endpoints.MapGroup("/wallet").WithTags("Wallet");
        GetWalletEndpoints.Map(wallet);
        PurchaseUnitsEndpoint.Map(wallet);

        var admin = endpoints.MapGroup("/admin/wallet").WithTags("Wallet (staff)");
        UnitRateEndpoints.Map(admin);
        AdjustWalletEndpoints.Map(admin);
    }

    public IEnumerable<ConsumerRegistration> Consumers() =>
    [
        ConsumerRegistry.For<PaymentSucceeded, PaymentSucceededConsumer, WalletDbContext>(Name),
        ConsumerRegistry.For<PaymentFailed, PaymentFailedConsumer, WalletDbContext>(Name),
    ];
}
