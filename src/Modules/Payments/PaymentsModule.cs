using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payments.Features.Checkout.CreatePaymentIntent;
using Plastoids.Modules.Payments.Features.Checkout.GetPaymentIntent;
using Plastoids.Modules.Payments.Features.Checkout.GetReceipt;
using Plastoids.Modules.Payments.Features.Checkout.HandleGatewayWebhook;
using Plastoids.Modules.Payments.Features.Checkout.ListMyPayments;
using Plastoids.Modules.Payments.Features.Checkout.VerifyPaymentIntent;
using Plastoids.Modules.Payments.Features.Invoices.CreateInvoice;
using Plastoids.Modules.Payments.Features.Invoices.GetInvoice;
using Plastoids.Modules.Payments.Features.Refunds.ApproveRefund;
using Plastoids.Modules.Payments.Features.Refunds.IssueRefund;
using Plastoids.Modules.Payments.Features.Admin.FeeRules;
using Plastoids.Modules.Payments.Features.Settlement.ImportSettlements;
using Plastoids.Modules.Payments.Features.Settlement.SweepEarnedRevenue;
using Plastoids.Modules.Payments.Jobs;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.Modules.Payments.Services;
using Quartz;

namespace Plastoids.Modules.Payments;

public sealed class PaymentsModule : IModule
{
    public string Name => "payments";
    public Assembly Assembly => typeof(PaymentsModule).Assembly;
    public Type DbContextType => typeof(PaymentsDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(PaymentSucceeded)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(PaymentFailed)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(InvoicePaid)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(RefundIssued)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FeeOptions>(configuration.GetSection(FeeOptions.SectionName));
        services.Configure<PaymentOptions>(configuration.GetSection(PaymentOptions.SectionName));
        services.AddScoped<IFeeRules, FeeRules>();
        services.AddScoped<IPaymentsAdminQueues, PaymentsAdminQueues>();
        services.AddScoped<PaymentProcessing>();
        services.AddScoped<RefundIssuer>();
        services.AddScoped<UnitCheckout>();
        services.AddScoped<IPaymentOperations, PaymentOperations>();
        services.AddScoped<InvoiceIssuer>();
        services.AddScoped<ISponsorInvoicing, SponsorInvoicing>();
        services.AddQuartz(q =>
        {
            var settle = new JobKey(SettlementImportJob.JobName);
            q.AddJob<SettlementImportJob>(j => j.WithIdentity(settle).StoreDurably());
            q.AddTrigger(t => t.ForJob(settle).WithIdentity(settle.Name + "-trigger").WithCronSchedule("0 0 4 * * ?"));
            var sweep = new JobKey(RevenueSweepJob.JobName);
            q.AddJob<RevenueSweepJob>(j => j.WithIdentity(sweep).StoreDurably());
            q.AddTrigger(t => t.ForJob(sweep).WithIdentity(sweep.Name + "-trigger").WithCronSchedule("0 0 5 ? * MON"));
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var payments = endpoints.MapGroup("/payments").WithTags("Payments");
        CreatePaymentIntentEndpoint.Map(payments);
        GetPaymentIntentEndpoint.Map(payments);
        VerifyPaymentIntentEndpoint.Map(payments);
        GetReceiptEndpoint.Map(payments);
        ListMyPaymentsEndpoint.Map(payments);
        HandleGatewayWebhookEndpoint.Map(payments);
        CreateInvoiceEndpoint.Map(payments);
        GetInvoiceEndpoint.Map(payments);

        var admin = endpoints.MapGroup("/admin/payments").WithTags("Payments (staff)");
        ImportSettlementsEndpoint.Map(admin);
        SweepEarnedRevenueEndpoint.Map(admin);
        IssueRefundEndpoint.Map(admin);
        ApproveRefundEndpoint.Map(admin);
        FeeRuleEndpoints.Map(admin);
    }

    public IEnumerable<ConsumerRegistration> Consumers() => [];
}
