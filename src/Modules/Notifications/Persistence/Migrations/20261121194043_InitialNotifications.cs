using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Notifications.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notifications_announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    cohort_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    author_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    body = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recipients = table.Column<int>(type: "int", nullable: false),
                    posted_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications_announcements", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notifications_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    notification_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    recipient_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    channel = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    destination = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    deferred_until = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    sent_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    provider_reference = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    error = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications_deliveries", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notifications_inbox",
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
                    table.PrimaryKey("pk_notifications_inbox", x => new { x.message_id, x.consumer });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notifications_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    recipient_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    template_key = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    dedup_key = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    subject = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    body = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    data_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    read_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications_notifications", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notifications_outbox",
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
                    table.PrimaryKey("pk_notifications_outbox", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notifications_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    channel_order_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    opted_out_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quiet_start = table.Column<TimeOnly>(type: "time(6)", nullable: true),
                    quiet_end = table.Column<TimeOnly>(type: "time(6)", nullable: true),
                    time_zone = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications_preferences", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notifications_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    key = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    channel = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    version = table.Column<int>(type: "int", nullable: false),
                    subject = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    body = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    approved = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_by = table.Column<Guid>(type: "binary(16)", nullable: false),
                    approved_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    approved_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications_templates", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notifications_thread_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    thread_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    author_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    body = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sent_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    flag_reason = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications_thread_messages", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notifications_threads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    learner_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    subject = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    context_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    last_message_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    message_count = table.Column<int>(type: "int", nullable: false),
                    is_flagged = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications_threads", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_announcements_cohort_id_posted_at",
                table: "notifications_announcements",
                columns: new[] { "cohort_id", "posted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_announcements_tenant_id",
                table: "notifications_announcements",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_deliveries_notification_id",
                table: "notifications_deliveries",
                column: "notification_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_deliveries_status_deferred_until",
                table: "notifications_deliveries",
                columns: new[] { "status", "deferred_until" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_deliveries_tenant_id",
                table: "notifications_deliveries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_notifications_dedup_key",
                table: "notifications_notifications",
                column: "dedup_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_notifications_recipient_id_created_at",
                table: "notifications_notifications",
                columns: new[] { "recipient_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_notifications_tenant_id",
                table: "notifications_notifications",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_outbox_published_at_occurred_at",
                table: "notifications_outbox",
                columns: new[] { "published_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_preferences_tenant_id",
                table: "notifications_preferences",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_preferences_user_id",
                table: "notifications_preferences",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_templates_key_channel_version",
                table: "notifications_templates",
                columns: new[] { "key", "channel", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_templates_tenant_id",
                table: "notifications_templates",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_thread_messages_tenant_id",
                table: "notifications_thread_messages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_thread_messages_thread_id_sent_at",
                table: "notifications_thread_messages",
                columns: new[] { "thread_id", "sent_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_threads_is_flagged",
                table: "notifications_threads",
                column: "is_flagged");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_threads_learner_user_id_last_message_at",
                table: "notifications_threads",
                columns: new[] { "learner_user_id", "last_message_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_threads_tenant_id",
                table: "notifications_threads",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_threads_tutor_user_id_last_message_at",
                table: "notifications_threads",
                columns: new[] { "tutor_user_id", "last_message_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications_announcements");

            migrationBuilder.DropTable(
                name: "notifications_deliveries");

            migrationBuilder.DropTable(
                name: "notifications_inbox");

            migrationBuilder.DropTable(
                name: "notifications_notifications");

            migrationBuilder.DropTable(
                name: "notifications_outbox");

            migrationBuilder.DropTable(
                name: "notifications_preferences");

            migrationBuilder.DropTable(
                name: "notifications_templates");

            migrationBuilder.DropTable(
                name: "notifications_thread_messages");

            migrationBuilder.DropTable(
                name: "notifications_threads");
        }
    }
}
