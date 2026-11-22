using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Ledger.Persistence.Migrations;

/// <summary>ADR 0018: two accounts for unit wallets. 2050 holds the cash collected for unsold units (per wallet); 4060 absorbs the gap between released unit value and applied price.</summary>
[DbContext(typeof(LedgerDbContext))]
[Migration("20261122010000_UnitAccounts")]
public partial class UnitAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO ledger_accounts (id, tenant_id, code, name, kind, sub_ledger_owner_type, created_at, updated_at, row_version) VALUES
            (UUID_TO_BIN('00000000-0000-7000-8000-000000002050'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '2050', 'Unit balances held (wallets)', 'Liability', 'wallet', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1),
            (UUID_TO_BIN('00000000-0000-7000-8000-000000004060'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '4060', 'Unit revaluation and rounding', 'Revenue', NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM ledger_accounts WHERE code IN ('2050', '4060');");
    }
}
