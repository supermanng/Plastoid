using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plastoids.Modules.Payments.Domain;

namespace Plastoids.Modules.Payments.Persistence.Configurations;

internal sealed class PaymentIntentConfiguration : IEntityTypeConfiguration<PaymentIntent>
{
    public void Configure(EntityTypeBuilder<PaymentIntent> b)
    {
        b.ToTable("payments_intents");
        b.Ignore(x => x.RefundableAmount);
        b.Ignore(x => x.SweepableCommission);
        b.Property(x => x.Purpose).HasMaxLength(16);
        b.Property(x => x.QuoteId).HasMaxLength(64);
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.Gateway).HasMaxLength(32);
        b.Property(x => x.GatewayReference).HasMaxLength(120);
        b.HasIndex(x => x.GatewayReference).IsUnique();
        b.Property(x => x.CheckoutUrl).HasMaxLength(1000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.CommissionRuleCode).HasMaxLength(48);
        b.Property(x => x.FailureReason).HasMaxLength(300);
        b.HasIndex(x => new { x.PayerId, x.CreatedAt });
        b.HasIndex(x => new { x.Status, x.SettledAt });
        b.HasIndex(x => new { x.Purpose, x.ReferenceId });
    }
}

internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.ToTable("payments_transactions");
        b.Property(x => x.Kind).HasMaxLength(16);
        b.Property(x => x.GatewayReference).HasMaxLength(120);
        b.HasIndex(x => new { x.PaymentIntentId, x.Kind });
        b.HasOne<PaymentIntent>().WithMany().HasForeignKey(x => x.PaymentIntentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> b)
    {
        b.ToTable("payments_refunds");
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.GatewayReference).HasMaxLength(120);
        b.HasIndex(x => x.PaymentIntentId);
        b.HasOne<PaymentIntent>().WithMany().HasForeignKey(x => x.PaymentIntentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.ToTable("payments_invoices");
        b.Ignore(x => x.Outstanding);
        b.Property(x => x.Number).HasMaxLength(32);
        b.HasIndex(x => x.Number).IsUnique();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.VirtualAccountBank).HasMaxLength(80);
        b.Property(x => x.VirtualAccountNumber).HasMaxLength(32);
        b.HasIndex(x => x.VirtualAccountNumber);
        b.Property(x => x.VirtualAccountName).HasMaxLength(160);
        b.HasIndex(x => new { x.OrganisationId, x.Status });
    }
}

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> b)
    {
        b.ToTable("payments_invoice_lines");
        b.Property(x => x.Kind).HasMaxLength(20);
        b.Property(x => x.Description).HasMaxLength(200);
        b.HasIndex(x => x.InvoiceId);
        b.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class IncomingTransferConfiguration : IEntityTypeConfiguration<IncomingTransfer>
{
    public void Configure(EntityTypeBuilder<IncomingTransfer> b)
    {
        b.ToTable("payments_incoming_transfers");
        b.Property(x => x.GatewayReference).HasMaxLength(120);
        b.HasIndex(x => x.GatewayReference).IsUnique();
        b.Property(x => x.VirtualAccountNumber).HasMaxLength(32);
        b.Property(x => x.Outcome).HasMaxLength(16);
        b.HasIndex(x => x.InvoiceId);
    }
}

internal sealed class GatewayEventConfiguration : IEntityTypeConfiguration<GatewayEvent>
{
    public void Configure(EntityTypeBuilder<GatewayEvent> b)
    {
        b.ToTable("payments_gateway_events");
        b.Property(x => x.Gateway).HasMaxLength(32);
        b.Property(x => x.EventId).HasMaxLength(160);
        b.HasIndex(x => new { x.Gateway, x.EventId }).IsUnique();
        b.Property(x => x.EventType).HasMaxLength(48);
        b.Property(x => x.Reference).HasMaxLength(120);
        b.Property(x => x.Outcome).HasMaxLength(48);
    }
}
