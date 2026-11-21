using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Sponsorship.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSponsorship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sponsorship_attendance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    programme_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    seat_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    enrolment_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    live_session_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    joined_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    left_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sponsorship_attendance", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sponsorship_funding_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    programme_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    invoice_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    received_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    amount_kobo = table.Column<long>(type: "bigint", nullable: false),
                    amount_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sponsorship_funding_receipts", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sponsorship_funnels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    programme_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    applied = table.Column<int>(type: "int", nullable: false),
                    offered = table.Column<int>(type: "int", nullable: false),
                    enrolled = table.Column<int>(type: "int", nullable: false),
                    active = table.Column<int>(type: "int", nullable: false),
                    completed = table.Column<int>(type: "int", nullable: false),
                    certified = table.Column<int>(type: "int", nullable: false),
                    refreshed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sponsorship_funnels", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sponsorship_inbox",
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
                    table.PrimaryKey("pk_sponsorship_inbox", x => new { x.message_id, x.consumer });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sponsorship_outbox",
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
                    table.PrimaryKey("pk_sponsorship_outbox", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sponsorship_programme_months",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    programme_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    month_index = table.Column<int>(type: "int", nullable: false),
                    filled_seats = table.Column<int>(type: "int", nullable: false),
                    posted_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    management_fee_kobo = table.Column<long>(type: "bigint", nullable: false),
                    management_fee_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tutor_earnings_kobo = table.Column<long>(type: "bigint", nullable: false),
                    tutor_earnings_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sponsorship_programme_months", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sponsorship_programmes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    organisation_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    sponsor_name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    objectives = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    course_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    offering_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    course_title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tutor_user_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    seat_count = table.Column<int>(type: "int", nullable: false),
                    sponsor_share_percent = table.Column<int>(type: "int", nullable: false),
                    eligibility_rules_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    entry_assessment_json = table.Column<string>(type: "json", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    scoring_rubric_json = table.Column<string>(type: "json", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    applications_open_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    applications_close_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    starts_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ends_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    selection_mode = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    unused_funds_policy = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quote_version = table.Column<int>(type: "int", nullable: false),
                    quoted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    quote_approved_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    quote_approved_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    invoice_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    review_status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reviewed_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    rules_lawful = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    review_notes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_plan_approved = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    payment_plan_json = table.Column<string>(type: "json", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    funded_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    seats_unlocked = table.Column<int>(type: "int", nullable: false),
                    published_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    published_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    closed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    closed_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    created_by = table.Column<Guid>(type: "binary(16)", nullable: false),
                    budget_kobo = table.Column<long>(type: "bigint", nullable: false),
                    budget_currency = table.Column<string>(type: "char(3)", nullable: false),
                    closeout_refund_kobo = table.Column<long>(type: "bigint", nullable: false),
                    closeout_refund_currency = table.Column<string>(type: "char(3)", nullable: false),
                    funded_amount_kobo = table.Column<long>(type: "bigint", nullable: false),
                    funded_amount_currency = table.Column<string>(type: "char(3)", nullable: false),
                    quote_management_fee_kobo = table.Column<long>(type: "bigint", nullable: false),
                    quote_management_fee_currency = table.Column<string>(type: "char(3)", nullable: false),
                    quote_seat_fee_kobo = table.Column<long>(type: "bigint", nullable: false),
                    quote_seat_fee_currency = table.Column<string>(type: "char(3)", nullable: false),
                    quote_total_kobo = table.Column<long>(type: "bigint", nullable: false),
                    quote_total_currency = table.Column<string>(type: "char(3)", nullable: false),
                    quote_tuition_kobo = table.Column<long>(type: "bigint", nullable: false),
                    quote_tuition_currency = table.Column<string>(type: "char(3)", nullable: false),
                    quote_tuition_per_seat_share_kobo = table.Column<long>(type: "bigint", nullable: false),
                    quote_tuition_per_seat_share_currency = table.Column<string>(type: "char(3)", nullable: false),
                    quote_vat_kobo = table.Column<long>(type: "bigint", nullable: false),
                    quote_vat_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tuition_per_seat_kobo = table.Column<long>(type: "bigint", nullable: false),
                    tuition_per_seat_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sponsorship_programmes", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sponsorship_seats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    programme_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    seat_no = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    application_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    learner_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    enrolment_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    reserved_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    filled_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    released_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    release_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fill_count = table.Column<int>(type: "int", nullable: false),
                    attendance_count = table.Column<int>(type: "int", nullable: false),
                    last_score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sponsorship_seats", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_attendance_enrolment_id_live_session_id",
                table: "sponsorship_attendance",
                columns: new[] { "enrolment_id", "live_session_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_attendance_programme_id_joined_at",
                table: "sponsorship_attendance",
                columns: new[] { "programme_id", "joined_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_attendance_tenant_id",
                table: "sponsorship_attendance",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_funding_receipts_invoice_id",
                table: "sponsorship_funding_receipts",
                column: "invoice_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_funding_receipts_programme_id",
                table: "sponsorship_funding_receipts",
                column: "programme_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_funding_receipts_tenant_id",
                table: "sponsorship_funding_receipts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_funnels_programme_id",
                table: "sponsorship_funnels",
                column: "programme_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_funnels_tenant_id",
                table: "sponsorship_funnels",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_outbox_published_at_occurred_at",
                table: "sponsorship_outbox",
                columns: new[] { "published_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_programme_months_programme_id_month_index",
                table: "sponsorship_programme_months",
                columns: new[] { "programme_id", "month_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_programme_months_tenant_id",
                table: "sponsorship_programme_months",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_programmes_invoice_id",
                table: "sponsorship_programmes",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_programmes_organisation_id",
                table: "sponsorship_programmes",
                column: "organisation_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_programmes_status_applications_close_at",
                table: "sponsorship_programmes",
                columns: new[] { "status", "applications_close_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_programmes_tenant_id",
                table: "sponsorship_programmes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seats_application_id",
                table: "sponsorship_seats",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seats_enrolment_id",
                table: "sponsorship_seats",
                column: "enrolment_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seats_learner_id",
                table: "sponsorship_seats",
                column: "learner_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seats_programme_id_seat_no",
                table: "sponsorship_seats",
                columns: new[] { "programme_id", "seat_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seats_programme_id_status",
                table: "sponsorship_seats",
                columns: new[] { "programme_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seats_tenant_id",
                table: "sponsorship_seats",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sponsorship_attendance");

            migrationBuilder.DropTable(
                name: "sponsorship_funding_receipts");

            migrationBuilder.DropTable(
                name: "sponsorship_funnels");

            migrationBuilder.DropTable(
                name: "sponsorship_inbox");

            migrationBuilder.DropTable(
                name: "sponsorship_outbox");

            migrationBuilder.DropTable(
                name: "sponsorship_programme_months");

            migrationBuilder.DropTable(
                name: "sponsorship_programmes");

            migrationBuilder.DropTable(
                name: "sponsorship_seats");
        }
    }
}
