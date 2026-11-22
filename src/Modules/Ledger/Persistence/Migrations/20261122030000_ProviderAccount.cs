using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Ledger.Persistence.Migrations;

/// <summary>ADR 0019: liability 2060 Provider payable, owed to organisations for sales of their courses (sub-ledger per organisation).</summary>
[DbContext(typeof(LedgerDbContext))]
[Migration("20261122030000_ProviderAccount")]
public partial class ProviderAccount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO ledger_accounts (id, tenant_id, code, name, kind, sub_ledger_owner_type, created_at, updated_at, row_version) VALUES
            (UUID_TO_BIN('00000000-0000-7000-8000-000000002060'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '2060', 'Provider payable (organisations)', 'Liability', 'organisation', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM ledger_accounts WHERE code = '2060';");
    }
}
