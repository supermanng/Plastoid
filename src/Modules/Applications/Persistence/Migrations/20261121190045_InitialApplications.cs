using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Applications.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "applications_appeals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    application_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    programme_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    learner_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    reason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    filed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    resolved_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    resolution = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications_appeals", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "applications_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    programme_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    learner_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    answers_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    nin_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    submitted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    merged_into_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    flag_reason = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    assessment_score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    assessment_passed = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    rubric_score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    total_score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    scored_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    scored_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_decision = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    decided_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    seat_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    offer_sent_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    offer_expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    offer_accepted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    co_pay_intent_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    co_pay_amount_kobo = table.Column<long>(type: "bigint", nullable: false),
                    co_pay_amount_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications_applications", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "applications_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    application_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    programme_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    learner_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    kind = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    decided_by = table.Column<Guid>(type: "binary(16)", nullable: false),
                    decided_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    source = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications_decisions", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "applications_entry_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    application_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    deadline_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    answers_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    max_score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    passed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications_entry_attempts", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "applications_inbox",
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
                    table.PrimaryKey("pk_applications_inbox", x => new { x.message_id, x.consumer });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "applications_outbox",
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
                    table.PrimaryKey("pk_applications_outbox", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "applications_programmes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    programme_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    seats_funded = table.Column<int>(type: "int", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications_programmes", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "applications_shortlist_approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    programme_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    approved_by = table.Column<Guid>(type: "binary(16)", nullable: false),
                    approved_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    delegated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    accepted_count = table.Column<int>(type: "int", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications_shortlist_approvals", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_applications_appeals_application_id_status",
                table: "applications_appeals",
                columns: new[] { "application_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_appeals_tenant_id",
                table: "applications_appeals",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_applications_co_pay_intent_id",
                table: "applications_applications",
                column: "co_pay_intent_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_applications_learner_id",
                table: "applications_applications",
                column: "learner_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_applications_programme_id_learner_id",
                table: "applications_applications",
                columns: new[] { "programme_id", "learner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_applications_programme_id_nin_hash",
                table: "applications_applications",
                columns: new[] { "programme_id", "nin_hash" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_applications_programme_id_phone_hash",
                table: "applications_applications",
                columns: new[] { "programme_id", "phone_hash" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_applications_programme_id_status",
                table: "applications_applications",
                columns: new[] { "programme_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_applications_seat_id",
                table: "applications_applications",
                column: "seat_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_applications_tenant_id",
                table: "applications_applications",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_decisions_application_id_decided_at",
                table: "applications_decisions",
                columns: new[] { "application_id", "decided_at" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_decisions_programme_id",
                table: "applications_decisions",
                column: "programme_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_decisions_tenant_id",
                table: "applications_decisions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_entry_attempts_application_id_status",
                table: "applications_entry_attempts",
                columns: new[] { "application_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_entry_attempts_tenant_id",
                table: "applications_entry_attempts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_outbox_published_at_occurred_at",
                table: "applications_outbox",
                columns: new[] { "published_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_programmes_programme_id",
                table: "applications_programmes",
                column: "programme_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_applications_programmes_tenant_id",
                table: "applications_programmes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_shortlist_approvals_programme_id",
                table: "applications_shortlist_approvals",
                column: "programme_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_applications_shortlist_approvals_tenant_id",
                table: "applications_shortlist_approvals",
                column: "tenant_id");

            // Every decision is append-only (story F4): no updates, no deletes.
            migrationBuilder.Sql("""
                CREATE TRIGGER applications_decisions_no_update BEFORE UPDATE ON applications_decisions
                FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'decisions are append-only';
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER applications_decisions_no_delete BEFORE DELETE ON applications_decisions
                FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'decisions are append-only';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS applications_decisions_no_update;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS applications_decisions_no_delete;");
            migrationBuilder.DropTable(
                name: "applications_appeals");

            migrationBuilder.DropTable(
                name: "applications_applications");

            migrationBuilder.DropTable(
                name: "applications_decisions");

            migrationBuilder.DropTable(
                name: "applications_entry_attempts");

            migrationBuilder.DropTable(
                name: "applications_inbox");

            migrationBuilder.DropTable(
                name: "applications_outbox");

            migrationBuilder.DropTable(
                name: "applications_programmes");

            migrationBuilder.DropTable(
                name: "applications_shortlist_approvals");
        }
    }
}
