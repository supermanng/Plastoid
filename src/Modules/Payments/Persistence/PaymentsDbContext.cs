using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Payments.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Payments.Persistence;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "payments";

    public DbSet<PaymentIntent> Intents => Set<PaymentIntent>();
    public DbSet<PaymentTransaction> Transactions => Set<PaymentTransaction>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<IncomingTransfer> Transfers => Set<IncomingTransfer>();
    public DbSet<GatewayEvent> GatewayEvents => Set<GatewayEvent>();
    public DbSet<FeeRule> FeeRules => Set<FeeRule>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeeRule>(b =>
        {
            b.ToTable("payments_fee_rules");
            b.Property(x => x.RuleCode).HasMaxLength(40);
            b.Property(x => x.Rationale).HasMaxLength(1000);
            b.Property(x => x.DecisionNote).HasMaxLength(1000);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.HasIndex(x => new { x.RuleCode, x.Status, x.EffectiveFrom });
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);
    }
}
