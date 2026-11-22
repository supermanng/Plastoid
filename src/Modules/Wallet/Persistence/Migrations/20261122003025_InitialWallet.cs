using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Wallet.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "wallet_inbox",
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
                    table.PrimaryKey("pk_wallet_inbox", x => new { x.message_id, x.consumer });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "wallet_lots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    wallet_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    units_total = table.Column<long>(type: "bigint", nullable: false),
                    units_remaining = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    acquired_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    value_remaining_kobo = table.Column<long>(type: "bigint", nullable: false),
                    value_remaining_currency = table.Column<string>(type: "char(3)", nullable: false),
                    value_total_kobo = table.Column<long>(type: "bigint", nullable: false),
                    value_total_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallet_lots", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "wallet_outbox",
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
                    table.PrimaryKey("pk_wallet_outbox", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "wallet_purchases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    wallet_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    buyer_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    units = table.Column<long>(type: "bigint", nullable: false),
                    rate_kobo = table.Column<long>(type: "bigint", nullable: false),
                    payment_intent_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    credited_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    failed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    amount_kobo = table.Column<long>(type: "bigint", nullable: false),
                    amount_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallet_purchases", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "wallet_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    wallet_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    kind = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    units = table.Column<long>(type: "bigint", nullable: false),
                    balance_after = table.Column<long>(type: "bigint", nullable: false),
                    reference = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    purpose = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    actor_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    value_kobo = table.Column<long>(type: "bigint", nullable: false),
                    value_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallet_transactions", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "wallet_unit_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    rate_kobo = table.Column<long>(type: "bigint", nullable: false),
                    version = table.Column<int>(type: "int", nullable: false),
                    effective_from = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rationale = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    proposed_by = table.Column<Guid>(type: "binary(16)", nullable: false),
                    proposed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    decided_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    decided_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    decision_note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallet_unit_rates", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "wallet_wallets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    owner_type = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    owner_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    balance = table.Column<long>(type: "bigint", nullable: false),
                    lifetime_purchased = table.Column<long>(type: "bigint", nullable: false),
                    lifetime_redeemed = table.Column<long>(type: "bigint", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallet_wallets", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_lots_reference",
                table: "wallet_lots",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wallet_lots_tenant_id",
                table: "wallet_lots",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_lots_wallet_id_units_remaining_acquired_at",
                table: "wallet_lots",
                columns: new[] { "wallet_id", "units_remaining", "acquired_at" });

            migrationBuilder.CreateIndex(
                name: "ix_wallet_outbox_published_at_occurred_at",
                table: "wallet_outbox",
                columns: new[] { "published_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_wallet_purchases_payment_intent_id",
                table: "wallet_purchases",
                column: "payment_intent_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wallet_purchases_tenant_id",
                table: "wallet_purchases",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_purchases_wallet_id_status",
                table: "wallet_purchases",
                columns: new[] { "wallet_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_wallet_transactions_reference",
                table: "wallet_transactions",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wallet_transactions_tenant_id",
                table: "wallet_transactions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_transactions_wallet_id_occurred_at",
                table: "wallet_transactions",
                columns: new[] { "wallet_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_wallet_unit_rates_status_effective_from",
                table: "wallet_unit_rates",
                columns: new[] { "status", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "ix_wallet_unit_rates_tenant_id",
                table: "wallet_unit_rates",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_unit_rates_version",
                table: "wallet_unit_rates",
                column: "version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wallet_wallets_owner_type_owner_id",
                table: "wallet_wallets",
                columns: new[] { "owner_type", "owner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wallet_wallets_tenant_id",
                table: "wallet_wallets",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wallet_inbox");

            migrationBuilder.DropTable(
                name: "wallet_lots");

            migrationBuilder.DropTable(
                name: "wallet_outbox");

            migrationBuilder.DropTable(
                name: "wallet_purchases");

            migrationBuilder.DropTable(
                name: "wallet_transactions");

            migrationBuilder.DropTable(
                name: "wallet_unit_rates");

            migrationBuilder.DropTable(
                name: "wallet_wallets");
        }
    }
}
