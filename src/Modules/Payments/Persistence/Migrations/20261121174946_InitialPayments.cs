using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Payments.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payments_gateway_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    gateway = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    event_id = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    event_type = table.Column<string>(type: "varchar(48)", maxLength: 48, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    received_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    outcome = table.Column<string>(type: "varchar(48)", maxLength: 48, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments_gateway_events", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payments_inbox",
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
                    table.PrimaryKey("pk_payments_inbox", x => new { x.message_id, x.consumer });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payments_incoming_transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    gateway_reference = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    virtual_account_number = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    received_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    invoice_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    outcome = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount_kobo = table.Column<long>(type: "bigint", nullable: false),
                    amount_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments_incoming_transfers", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payments_intents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    payer_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    purpose = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    quote_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gateway = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gateway_reference = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    checkout_url = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payee_user_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    commission_bps = table.Column<int>(type: "int", nullable: false),
                    commission_rule_code = table.Column<string>(type: "varchar(48)", maxLength: 48, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    succeeded_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    failed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    failure_reason = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    settled_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    swept_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ledger_entry_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    amount_kobo = table.Column<long>(type: "bigint", nullable: false),
                    amount_currency = table.Column<string>(type: "char(3)", nullable: false),
                    commission_kobo = table.Column<long>(type: "bigint", nullable: false),
                    commission_currency = table.Column<string>(type: "char(3)", nullable: false),
                    gateway_fee_kobo = table.Column<long>(type: "bigint", nullable: false),
                    gateway_fee_currency = table.Column<string>(type: "char(3)", nullable: false),
                    payee_net_kobo = table.Column<long>(type: "bigint", nullable: false),
                    payee_net_currency = table.Column<string>(type: "char(3)", nullable: false),
                    refunded_amount_kobo = table.Column<long>(type: "bigint", nullable: false),
                    refunded_amount_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments_intents", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payments_invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    organisation_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    programme_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    number = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    issued_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    due_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    paid_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    virtual_account_bank = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    virtual_account_number = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    virtual_account_name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ledger_entry_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    created_by = table.Column<Guid>(type: "binary(16)", nullable: false),
                    paid_amount_kobo = table.Column<long>(type: "bigint", nullable: false),
                    paid_amount_currency = table.Column<string>(type: "char(3)", nullable: false),
                    subtotal_kobo = table.Column<long>(type: "bigint", nullable: false),
                    subtotal_currency = table.Column<string>(type: "char(3)", nullable: false),
                    total_kobo = table.Column<long>(type: "bigint", nullable: false),
                    total_currency = table.Column<string>(type: "char(3)", nullable: false),
                    vat_kobo = table.Column<long>(type: "bigint", nullable: false),
                    vat_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments_invoices", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payments_outbox",
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
                    table.PrimaryKey("pk_payments_outbox", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payments_refunds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    payment_intent_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    requested_by = table.Column<Guid>(type: "binary(16)", nullable: false),
                    approved_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    issued_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    gateway_reference = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ledger_entry_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    amount_kobo = table.Column<long>(type: "bigint", nullable: false),
                    amount_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments_refunds", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_refunds_payments_intents_payment_intent_id",
                        column: x => x.payment_intent_id,
                        principalTable: "payments_intents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payments_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    payment_intent_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    kind = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gateway_reference = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ledger_entry_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    amount_kobo = table.Column<long>(type: "bigint", nullable: false),
                    amount_currency = table.Column<string>(type: "char(3)", nullable: false),
                    fee_kobo = table.Column<long>(type: "bigint", nullable: false),
                    fee_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_transactions_payments_intents_payment_intent_id",
                        column: x => x.payment_intent_id,
                        principalTable: "payments_intents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payments_invoice_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    invoice_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    kind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    line = table.Column<int>(type: "int", nullable: false),
                    amount_kobo = table.Column<long>(type: "bigint", nullable: false),
                    amount_currency = table.Column<string>(type: "char(3)", nullable: false),
                    unit_amount_kobo = table.Column<long>(type: "bigint", nullable: false),
                    unit_amount_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments_invoice_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_invoice_lines_payments_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "payments_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_payments_gateway_events_gateway_event_id",
                table: "payments_gateway_events",
                columns: new[] { "gateway", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_gateway_events_tenant_id",
                table: "payments_gateway_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_incoming_transfers_gateway_reference",
                table: "payments_incoming_transfers",
                column: "gateway_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_incoming_transfers_invoice_id",
                table: "payments_incoming_transfers",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_incoming_transfers_tenant_id",
                table: "payments_incoming_transfers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_intents_gateway_reference",
                table: "payments_intents",
                column: "gateway_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_intents_payer_id_created_at",
                table: "payments_intents",
                columns: new[] { "payer_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_intents_purpose_reference_id",
                table: "payments_intents",
                columns: new[] { "purpose", "reference_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_intents_status_settled_at",
                table: "payments_intents",
                columns: new[] { "status", "settled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_intents_tenant_id",
                table: "payments_intents",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_invoice_lines_invoice_id",
                table: "payments_invoice_lines",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_invoice_lines_tenant_id",
                table: "payments_invoice_lines",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_invoices_number",
                table: "payments_invoices",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_invoices_organisation_id_status",
                table: "payments_invoices",
                columns: new[] { "organisation_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_invoices_tenant_id",
                table: "payments_invoices",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_invoices_virtual_account_number",
                table: "payments_invoices",
                column: "virtual_account_number");

            migrationBuilder.CreateIndex(
                name: "ix_payments_outbox_published_at_occurred_at",
                table: "payments_outbox",
                columns: new[] { "published_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_refunds_payment_intent_id",
                table: "payments_refunds",
                column: "payment_intent_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_refunds_tenant_id",
                table: "payments_refunds",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_transactions_payment_intent_id_kind",
                table: "payments_transactions",
                columns: new[] { "payment_intent_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_transactions_tenant_id",
                table: "payments_transactions",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payments_gateway_events");

            migrationBuilder.DropTable(
                name: "payments_inbox");

            migrationBuilder.DropTable(
                name: "payments_incoming_transfers");

            migrationBuilder.DropTable(
                name: "payments_invoice_lines");

            migrationBuilder.DropTable(
                name: "payments_outbox");

            migrationBuilder.DropTable(
                name: "payments_refunds");

            migrationBuilder.DropTable(
                name: "payments_transactions");

            migrationBuilder.DropTable(
                name: "payments_invoices");

            migrationBuilder.DropTable(
                name: "payments_intents");
        }
    }
}
