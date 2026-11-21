using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Booking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "booking_bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    learner_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    format = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    slot_start = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    slot_end = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    duration_minutes = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_intent_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    package_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    slot_key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    confirmed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    cancelled_by = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    cancellation_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    refund_percent = table.Column<int>(type: "int", nullable: false),
                    rescheduled_from = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    reminder24sent_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    reminder1sent_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    price_kobo = table.Column<long>(type: "bigint", nullable: false),
                    price_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_bookings", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "booking_earning_holds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    booking_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    releases_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    released_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    amount_kobo = table.Column<long>(type: "bigint", nullable: false),
                    amount_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_earning_holds", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "booking_inbox",
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
                    table.PrimaryKey("pk_booking_inbox", x => new { x.message_id, x.consumer });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "booking_outbox",
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
                    table.PrimaryKey("pk_booking_outbox", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "booking_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    learner_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    format = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    duration_minutes = table.Column<int>(type: "int", nullable: false),
                    sessions_total = table.Column<int>(type: "int", nullable: false),
                    sessions_used = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_intent_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    activated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    price_per_session_kobo = table.Column<long>(type: "bigint", nullable: false),
                    price_per_session_currency = table.Column<string>(type: "char(3)", nullable: false),
                    total_kobo = table.Column<long>(type: "bigint", nullable: false),
                    total_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_packages", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "booking_tutor_strikes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    booking_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    reason = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_tutor_strikes", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_booking_bookings_learner_id_slot_start",
                table: "booking_bookings",
                columns: new[] { "learner_id", "slot_start" });

            migrationBuilder.CreateIndex(
                name: "ix_booking_bookings_slot_key",
                table: "booking_bookings",
                column: "slot_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_booking_bookings_status_slot_start",
                table: "booking_bookings",
                columns: new[] { "status", "slot_start" });

            migrationBuilder.CreateIndex(
                name: "ix_booking_bookings_tenant_id",
                table: "booking_bookings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_bookings_tutor_id_slot_start",
                table: "booking_bookings",
                columns: new[] { "tutor_id", "slot_start" });

            migrationBuilder.CreateIndex(
                name: "ix_booking_earning_holds_booking_id",
                table: "booking_earning_holds",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_booking_earning_holds_status_releases_at",
                table: "booking_earning_holds",
                columns: new[] { "status", "releases_at" });

            migrationBuilder.CreateIndex(
                name: "ix_booking_earning_holds_tenant_id",
                table: "booking_earning_holds",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_outbox_published_at_occurred_at",
                table: "booking_outbox",
                columns: new[] { "published_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_booking_packages_learner_id_tutor_id",
                table: "booking_packages",
                columns: new[] { "learner_id", "tutor_id" });

            migrationBuilder.CreateIndex(
                name: "ix_booking_packages_tenant_id",
                table: "booking_packages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_tutor_strikes_tenant_id",
                table: "booking_tutor_strikes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_tutor_strikes_tutor_id_occurred_at",
                table: "booking_tutor_strikes",
                columns: new[] { "tutor_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_bookings");

            migrationBuilder.DropTable(
                name: "booking_earning_holds");

            migrationBuilder.DropTable(
                name: "booking_inbox");

            migrationBuilder.DropTable(
                name: "booking_outbox");

            migrationBuilder.DropTable(
                name: "booking_packages");

            migrationBuilder.DropTable(
                name: "booking_tutor_strikes");
        }
    }
}
