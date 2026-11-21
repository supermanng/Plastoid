using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Lms.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialLms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "lms_inbox",
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
                    table.PrimaryKey("pk_lms_inbox", x => new { x.message_id, x.consumer });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "lms_live_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    source = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    booking_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    cohort_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    host_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    participant_user_ids_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    starts_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ends_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    provider = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    room_id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    room_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recording_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lms_live_sessions", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "lms_outbox",
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
                    table.PrimaryKey("pk_lms_outbox", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "lms_attendance_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    live_session_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    joined_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    left_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    duration_seconds = table.Column<int>(type: "int", nullable: false),
                    provider_event_id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lms_attendance_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_lms_attendance_events_lms_live_sessions_live_session_id",
                        column: x => x.live_session_id,
                        principalTable: "lms_live_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_lms_attendance_events_live_session_id_user_id",
                table: "lms_attendance_events",
                columns: new[] { "live_session_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lms_attendance_events_provider_event_id",
                table: "lms_attendance_events",
                column: "provider_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lms_attendance_events_tenant_id",
                table: "lms_attendance_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lms_live_sessions_booking_id",
                table: "lms_live_sessions",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lms_live_sessions_host_user_id_starts_at",
                table: "lms_live_sessions",
                columns: new[] { "host_user_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lms_live_sessions_room_id",
                table: "lms_live_sessions",
                column: "room_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lms_live_sessions_status_starts_at",
                table: "lms_live_sessions",
                columns: new[] { "status", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lms_live_sessions_tenant_id",
                table: "lms_live_sessions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lms_outbox_published_at_occurred_at",
                table: "lms_outbox",
                columns: new[] { "published_at", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lms_attendance_events");

            migrationBuilder.DropTable(
                name: "lms_inbox");

            migrationBuilder.DropTable(
                name: "lms_outbox");

            migrationBuilder.DropTable(
                name: "lms_live_sessions");
        }
    }
}
