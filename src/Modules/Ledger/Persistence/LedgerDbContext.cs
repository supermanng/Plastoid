using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Ledger.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Ledger.Persistence;

public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options, ICurrentTenant tenant) : ModuleDbContext(options, tenant)
{
    public override string ModulePrefix => "ledger";

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> Entries => Set<JournalEntry>();
    public DbSet<Posting> Postings => Set<Posting>();
    public DbSet<LedgerControl> Controls => Set<LedgerControl>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(b =>
        {
            b.ToTable("ledger_accounts");
            b.Property(x => x.Code).HasMaxLength(8);
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Name).HasMaxLength(120);
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.SubLedgerOwnerType).HasMaxLength(16);
        });
        modelBuilder.Entity<JournalEntry>(b =>
        {
            b.ToTable("ledger_journal_entries");
            b.Property(x => x.Reference).HasMaxLength(160);
            b.HasIndex(x => x.Reference).IsUnique();
            b.Property(x => x.Kind).HasMaxLength(48);
            b.Property(x => x.Description).HasMaxLength(300);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.HasIndex(x => x.OccurredAt);
        });
        modelBuilder.Entity<Posting>(b =>
        {
            b.ToTable("ledger_postings", t => t.HasCheckConstraint("ck_ledger_postings_one_side", "(debit_kobo = 0 OR credit_kobo = 0) AND debit_kobo >= 0 AND credit_kobo >= 0"));
            b.Property(x => x.AccountCode).HasMaxLength(8);
            b.Property(x => x.OwnerType).HasMaxLength(16);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.HasIndex(x => x.JournalEntryId);
            b.HasIndex(x => new { x.AccountCode, x.OwnerId });
            b.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LedgerControl>(b =>
        {
            b.ToTable("ledger_controls");
            b.Property(x => x.FreezeReason).HasMaxLength(300);
        });
    }
}
