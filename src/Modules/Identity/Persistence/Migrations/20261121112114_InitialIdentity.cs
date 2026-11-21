using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "identity_inbox",
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
                    table.PrimaryKey("pk_identity_inbox", x => new { x.message_id, x.consumer });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "identity_outbox",
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
                    table.PrimaryKey("pk_identity_outbox", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_identity_outbox_published_at_occurred_at",
                table: "identity_outbox",
                columns: new[] { "published_at", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "identity_inbox");

            migrationBuilder.DropTable(
                name: "identity_outbox");
        }
    }
}
