using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Payouts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payouts_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    reference = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by = table.Column<Guid>(type: "binary(16)", nullable: false),
                    approved_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    approved_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    rejected_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    rejection_reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    submitted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    count = table.Column<int>(type: "int", nullable: false),
                    total_kobo = table.Column<long>(type: "bigint", nullable: false),
                    total_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payouts_batches", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payouts_earnings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    source_type = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    reference_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    source_key = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    earned_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    releases_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    released_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    commission_kobo = table.Column<long>(type: "bigint", nullable: false),
                    commission_currency = table.Column<string>(type: "char(3)", nullable: false),
                    gross_kobo = table.Column<long>(type: "bigint", nullable: false),
                    gross_currency = table.Column<string>(type: "char(3)", nullable: false),
                    net_kobo = table.Column<long>(type: "bigint", nullable: false),
                    net_currency = table.Column<string>(type: "char(3)", nullable: false),
                    refunded_net_kobo = table.Column<long>(type: "bigint", nullable: false),
                    refunded_net_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payouts_earnings", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payouts_holds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    placed_by = table.Column<Guid>(type: "binary(16)", nullable: false),
                    placed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    releases_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    released_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    released_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    resolution = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payouts_holds", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payouts_inbox",
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
                    table.PrimaryKey("pk_payouts_inbox", x => new { x.message_id, x.consumer });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payouts_outbox",
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
                    table.PrimaryKey("pk_payouts_outbox", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payouts_payouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    batch_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    bank_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    account_number_masked = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    account_name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_reference = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    bank_reference = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    failure_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    submitted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    settled_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    failed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ledger_entry_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    reversal_entry_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    amount_kobo = table.Column<long>(type: "bigint", nullable: false),
                    amount_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payouts_payouts", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payouts_provider_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    provider = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    event_id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    event_type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    outcome = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    received_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payouts_provider_events", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_batches_reference",
                table: "payouts_batches",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payouts_batches_status",
                table: "payouts_batches",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_batches_tenant_id",
                table: "payouts_batches",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_earnings_reference_id",
                table: "payouts_earnings",
                column: "reference_id");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_earnings_source_id",
                table: "payouts_earnings",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_earnings_source_key",
                table: "payouts_earnings",
                column: "source_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payouts_earnings_status_releases_at",
                table: "payouts_earnings",
                columns: new[] { "status", "releases_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payouts_earnings_tenant_id",
                table: "payouts_earnings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_earnings_tutor_user_id_status",
                table: "payouts_earnings",
                columns: new[] { "tutor_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_payouts_holds_tenant_id",
                table: "payouts_holds",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_holds_tutor_user_id_released_at",
                table: "payouts_holds",
                columns: new[] { "tutor_user_id", "released_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payouts_outbox_published_at_occurred_at",
                table: "payouts_outbox",
                columns: new[] { "published_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payouts_payouts_batch_id",
                table: "payouts_payouts",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_payouts_provider_reference",
                table: "payouts_payouts",
                column: "provider_reference");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_payouts_tenant_id",
                table: "payouts_payouts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_payouts_tutor_user_id_status",
                table: "payouts_payouts",
                columns: new[] { "tutor_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_payouts_provider_events_provider_event_id",
                table: "payouts_provider_events",
                columns: new[] { "provider", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payouts_provider_events_tenant_id",
                table: "payouts_provider_events",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payouts_batches");

            migrationBuilder.DropTable(
                name: "payouts_earnings");

            migrationBuilder.DropTable(
                name: "payouts_holds");

            migrationBuilder.DropTable(
                name: "payouts_inbox");

            migrationBuilder.DropTable(
                name: "payouts_outbox");

            migrationBuilder.DropTable(
                name: "payouts_payouts");

            migrationBuilder.DropTable(
                name: "payouts_provider_events");
        }
    }
}
