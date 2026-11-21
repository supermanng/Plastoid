using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Infrastructure.Persistence.Shared.Migrations
{
    /// <inheritdoc />
    public partial class InitialShared : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "shared_idempotency_keys",
                columns: table => new
                {
                    key = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    actor_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    request_type = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    request_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status_code = table.Column<int>(type: "int", nullable: false),
                    response_body = table.Column<string>(type: "json", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shared_idempotency_keys", x => new { x.key, x.actor_id });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "shared_inbox",
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
                    table.PrimaryKey("pk_shared_inbox", x => new { x.message_id, x.consumer });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "shared_outbox",
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
                    table.PrimaryKey("pk_shared_outbox", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_shared_idempotency_keys_expires_at",
                table: "shared_idempotency_keys",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_shared_outbox_published_at_occurred_at",
                table: "shared_outbox",
                columns: new[] { "published_at", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shared_idempotency_keys");

            migrationBuilder.DropTable(
                name: "shared_inbox");

            migrationBuilder.DropTable(
                name: "shared_outbox");
        }
    }
}
