using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Tutors.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTutors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tutors_inbox",
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
                    table.PrimaryKey("pk_tutors_inbox", x => new { x.message_id, x.consumer });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tutors_outbox",
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
                    table.PrimaryKey("pk_tutors_outbox", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tutors_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    headline = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    bio = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    subjects_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    languages_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    formats_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    time_zone = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    level = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quality_score = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    sessions_delivered = table.Column<int>(type: "int", nullable: false),
                    enrolments_taught = table.Column<int>(type: "int", nullable: false),
                    rating_average = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    rating_count = table.Column<int>(type: "int", nullable: false),
                    code_of_conduct_accepted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    identity_verified_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    verified_full_name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    qualifications_reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    demo_lesson_blob_key = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    demo_lesson_submitted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    subject_reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    verified_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    calendar_connected = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    calendar_account = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    hourly_rate_kobo = table.Column<long>(type: "bigint", nullable: false),
                    hourly_rate_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tutors_profiles", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tutors_availability",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_profile_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    kind = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    day = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    start = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    end = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    available = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tutors_availability", x => x.id);
                    table.ForeignKey(
                        name: "fk_tutors_availability_profiles_tutor_profile_id",
                        column: x => x.tutor_profile_id,
                        principalTable: "tutors_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tutors_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_profile_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    type = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    issuer = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    evidence_blob_key = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reviewed_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    review_note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tutors_credentials", x => x.id);
                    table.ForeignKey(
                        name: "fk_tutors_credentials_profiles_tutor_profile_id",
                        column: x => x.tutor_profile_id,
                        principalTable: "tutors_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tutors_payout_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_profile_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    bank_code = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    account_number_ciphertext = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    account_number_masked = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    account_name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name_matched = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    verified_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tutors_payout_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_tutors_payout_accounts_profiles_tutor_profile_id",
                        column: x => x.tutor_profile_id,
                        principalTable: "tutors_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tutors_price_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_profile_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    format = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    duration_minutes = table.Column<int>(type: "int", nullable: false),
                    max_learners = table.Column<int>(type: "int", nullable: true),
                    price_kobo = table.Column<long>(type: "bigint", nullable: false),
                    price_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tutors_price_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_tutors_price_rules_profiles_tutor_profile_id",
                        column: x => x.tutor_profile_id,
                        principalTable: "tutors_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tutors_review_queue",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_profile_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    kind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    enqueued_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    due_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    sla_alerted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tutors_review_queue", x => x.id);
                    table.ForeignKey(
                        name: "fk_tutors_review_queue_profiles_tutor_profile_id",
                        column: x => x.tutor_profile_id,
                        principalTable: "tutors_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tutors_verification_checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_profile_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    type = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    result = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    detail = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    checked_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    checked_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tutors_verification_checks", x => x.id);
                    table.ForeignKey(
                        name: "fk_tutors_verification_checks_tutors_profiles_tutor_profile_id",
                        column: x => x.tutor_profile_id,
                        principalTable: "tutors_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_tutors_availability_tenant_id",
                table: "tutors_availability",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutors_availability_tutor_profile_id",
                table: "tutors_availability",
                column: "tutor_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutors_credentials_tenant_id",
                table: "tutors_credentials",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutors_credentials_tutor_profile_id_status",
                table: "tutors_credentials",
                columns: new[] { "tutor_profile_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_tutors_outbox_published_at_occurred_at",
                table: "tutors_outbox",
                columns: new[] { "published_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_tutors_payout_accounts_tenant_id",
                table: "tutors_payout_accounts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutors_payout_accounts_tutor_profile_id",
                table: "tutors_payout_accounts",
                column: "tutor_profile_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tutors_price_rules_tenant_id",
                table: "tutors_price_rules",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutors_price_rules_tutor_profile_id_format_duration_minutes",
                table: "tutors_price_rules",
                columns: new[] { "tutor_profile_id", "format", "duration_minutes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ft_tutors_profiles_headline_bio",
                table: "tutors_profiles",
                columns: new[] { "headline", "bio" })
                .Annotation("MySql:FullTextIndex", true);

            migrationBuilder.CreateIndex(
                name: "ix_tutors_profiles_status_level",
                table: "tutors_profiles",
                columns: new[] { "status", "level" });

            migrationBuilder.CreateIndex(
                name: "ix_tutors_profiles_tenant_id",
                table: "tutors_profiles",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutors_profiles_user_id",
                table: "tutors_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tutors_review_queue_completed_at_due_at",
                table: "tutors_review_queue",
                columns: new[] { "completed_at", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_tutors_review_queue_kind_reference_id",
                table: "tutors_review_queue",
                columns: new[] { "kind", "reference_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tutors_review_queue_tenant_id",
                table: "tutors_review_queue",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutors_review_queue_tutor_profile_id",
                table: "tutors_review_queue",
                column: "tutor_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutors_verification_checks_tenant_id",
                table: "tutors_verification_checks",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutors_verification_checks_tutor_profile_id_type",
                table: "tutors_verification_checks",
                columns: new[] { "tutor_profile_id", "type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tutors_availability");

            migrationBuilder.DropTable(
                name: "tutors_credentials");

            migrationBuilder.DropTable(
                name: "tutors_inbox");

            migrationBuilder.DropTable(
                name: "tutors_outbox");

            migrationBuilder.DropTable(
                name: "tutors_payout_accounts");

            migrationBuilder.DropTable(
                name: "tutors_price_rules");

            migrationBuilder.DropTable(
                name: "tutors_review_queue");

            migrationBuilder.DropTable(
                name: "tutors_verification_checks");

            migrationBuilder.DropTable(
                name: "tutors_profiles");
        }
    }
}
