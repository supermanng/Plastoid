using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Certificates.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "certificates_certificates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    enrolment_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    learner_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    learner_name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    course_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    course_title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    offering_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    version_no = table.Column<int>(type: "int", nullable: false),
                    final_score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    public_code = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    template_key = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    issued_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    revocation_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    revoked_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_certificates_certificates", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "certificates_inbox",
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
                    table.PrimaryKey("pk_certificates_inbox", x => new { x.message_id, x.consumer });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "certificates_outbox",
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
                    table.PrimaryKey("pk_certificates_outbox", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "certificates_verification_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    public_code = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    outcome = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ip = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    verified_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_certificates_verification_logs", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_certificates_enrolment_id",
                table: "certificates_certificates",
                column: "enrolment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_certificates_certificates_learner_id",
                table: "certificates_certificates",
                column: "learner_id");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_certificates_public_code",
                table: "certificates_certificates",
                column: "public_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_certificates_certificates_tenant_id",
                table: "certificates_certificates",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_outbox_published_at_occurred_at",
                table: "certificates_outbox",
                columns: new[] { "published_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_certificates_verification_logs_public_code_verified_at",
                table: "certificates_verification_logs",
                columns: new[] { "public_code", "verified_at" });

            migrationBuilder.CreateIndex(
                name: "ix_certificates_verification_logs_tenant_id",
                table: "certificates_verification_logs",
                column: "tenant_id");

            // Immutable once issued (blueprint §9.14): only the revocation columns may change, never a delete.
            migrationBuilder.Sql("""
                CREATE TRIGGER certificates_certificates_immutable BEFORE UPDATE ON certificates_certificates
                FOR EACH ROW BEGIN
                    IF NEW.public_code <> OLD.public_code OR NEW.hash <> OLD.hash OR NEW.enrolment_id <> OLD.enrolment_id OR NEW.learner_id <> OLD.learner_id
                       OR NEW.course_id <> OLD.course_id OR NEW.course_title <> OLD.course_title OR NEW.learner_name <> OLD.learner_name
                       OR NEW.issued_at <> OLD.issued_at OR NEW.final_score <> OLD.final_score THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'certificates are immutable once issued';
                    END IF;
                END
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER certificates_certificates_no_delete BEFORE DELETE ON certificates_certificates
                FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'certificates are immutable once issued';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS certificates_certificates_immutable;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS certificates_certificates_no_delete;");
            migrationBuilder.DropTable(
                name: "certificates_certificates");

            migrationBuilder.DropTable(
                name: "certificates_inbox");

            migrationBuilder.DropTable(
                name: "certificates_outbox");

            migrationBuilder.DropTable(
                name: "certificates_verification_logs");
        }
    }
}
