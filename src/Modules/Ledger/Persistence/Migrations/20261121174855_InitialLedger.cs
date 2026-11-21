using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Ledger.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ledger_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    code = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    kind = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sub_ledger_owner_type = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_accounts", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ledger_controls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    payouts_frozen = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    freeze_reason = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_reconciled_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_assets_kobo = table.Column<long>(type: "bigint", nullable: true),
                    last_liabilities_kobo = table.Column<long>(type: "bigint", nullable: true),
                    last_verified_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_unbalanced_entries = table.Column<int>(type: "int", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_controls", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ledger_inbox",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    consumer = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    received_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    processed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_inbox", x => new { x.message_id, x.consumer });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ledger_journal_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    reference = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    kind = table.Column<string>(type: "varchar(48)", maxLength: 48, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    posted_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    total_debit_kobo = table.Column<long>(type: "bigint", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_journal_entries", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ledger_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    topic = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    partition_key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    event_type = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    event_version = table.Column<int>(type: "int", nullable: false),
                    payload = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    headers = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    published_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    last_error = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_outbox", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ledger_postings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    journal_entry_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    account_code = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    owner_type = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    owner_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    debit_kobo = table.Column<long>(type: "bigint", nullable: false),
                    credit_kobo = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    line = table.Column<int>(type: "int", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_postings", x => x.id);
                    table.CheckConstraint("ck_ledger_postings_one_side", "(debit_kobo = 0 OR credit_kobo = 0) AND debit_kobo >= 0 AND credit_kobo >= 0");
                    table.ForeignKey(
                        name: "fk_ledger_postings_ledger_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "ledger_journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_accounts_code",
                table: "ledger_accounts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ledger_accounts_tenant_id",
                table: "ledger_accounts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_controls_tenant_id",
                table: "ledger_controls",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_journal_entries_occurred_at",
                table: "ledger_journal_entries",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_journal_entries_reference",
                table: "ledger_journal_entries",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ledger_journal_entries_tenant_id",
                table: "ledger_journal_entries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_outbox_published_at_occurred_at",
                table: "ledger_outbox",
                columns: new[] { "published_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_postings_account_code_owner_id",
                table: "ledger_postings",
                columns: new[] { "account_code", "owner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_postings_journal_entry_id",
                table: "ledger_postings",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_postings_tenant_id",
                table: "ledger_postings",
                column: "tenant_id");

            // Chart of accounts (blueprint §18 / build prompt §2.9). Ids are fixed so environments agree.
            migrationBuilder.Sql("""
                INSERT INTO ledger_accounts (id, tenant_id, code, name, kind, sub_ledger_owner_type, created_at, updated_at, row_version) VALUES
                (UUID_TO_BIN('00000000-0000-7000-8000-000000001010'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '1010', 'Bank — operating', 'Asset', NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1),
                (UUID_TO_BIN('00000000-0000-7000-8000-000000001020'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '1020', 'Bank — client funds (segregated)', 'Asset', NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1),
                (UUID_TO_BIN('00000000-0000-7000-8000-000000001030'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '1030', 'Gateway receivable (in transit)', 'Asset', NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1),
                (UUID_TO_BIN('00000000-0000-7000-8000-000000002010'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '2010', 'Tutor payable', 'Liability', 'tutor', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1),
                (UUID_TO_BIN('00000000-0000-7000-8000-000000002020'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '2020', 'Sponsor programme funds held', 'Liability', 'programme', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1),
                (UUID_TO_BIN('00000000-0000-7000-8000-000000002030'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '2030', 'Learner refund credit', 'Liability', 'learner', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1),
                (UUID_TO_BIN('00000000-0000-7000-8000-000000002040'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '2040', 'VAT payable', 'Liability', NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1),
                (UUID_TO_BIN('00000000-0000-7000-8000-000000004010'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '4010', 'Course commission', 'Revenue', NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1),
                (UUID_TO_BIN('00000000-0000-7000-8000-000000004020'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '4020', 'Tutoring commission', 'Revenue', NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1),
                (UUID_TO_BIN('00000000-0000-7000-8000-000000004030'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '4030', 'Programme management fee', 'Revenue', NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1),
                (UUID_TO_BIN('00000000-0000-7000-8000-000000004040'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '4040', 'Platform seat fee', 'Revenue', NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1),
                (UUID_TO_BIN('00000000-0000-7000-8000-000000004050'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '4050', 'Subscriptions', 'Revenue', NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1),
                (UUID_TO_BIN('00000000-0000-7000-8000-000000005010'), UUID_TO_BIN('00000000-0000-7000-8000-000000000001'), '5010', 'Payment processing and transfer fees', 'Expense', NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1);
                """);

            // Append-only: journal entries and postings are never edited; corrections are reversing entries.
            foreach (var table in new[] { "ledger_journal_entries", "ledger_postings" })
            {
                migrationBuilder.Sql($"CREATE TRIGGER {table}_no_update BEFORE UPDATE ON {table} FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '{table} is append-only';");
                migrationBuilder.Sql($"CREATE TRIGGER {table}_no_delete BEFORE DELETE ON {table} FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '{table} is append-only';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS ledger_journal_entries_no_update;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS ledger_journal_entries_no_delete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS ledger_postings_no_update;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS ledger_postings_no_delete;");
            migrationBuilder.DropTable(
                name: "ledger_accounts");

            migrationBuilder.DropTable(
                name: "ledger_controls");

            migrationBuilder.DropTable(
                name: "ledger_inbox");

            migrationBuilder.DropTable(
                name: "ledger_outbox");

            migrationBuilder.DropTable(
                name: "ledger_postings");

            migrationBuilder.DropTable(
                name: "ledger_journal_entries");
        }
    }
}
