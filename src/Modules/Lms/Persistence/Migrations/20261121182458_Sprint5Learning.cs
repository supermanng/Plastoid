using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Lms.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint5Learning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lms_cohorts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    course_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    offering_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    starts_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ends_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    capacity = table.Column<int>(type: "int", nullable: false),
                    enrolled = table.Column<int>(type: "int", nullable: false),
                    programme_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lms_cohorts", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "lms_enrolments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    learner_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    course_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    offering_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    cohort_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    version_no = table.Column<int>(type: "int", nullable: false),
                    funding_source = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    funding_source_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    source_key = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    progress_pct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    required_lessons = table.Column<int>(type: "int", nullable: false),
                    completed_lessons = table.Column<int>(type: "int", nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    final_score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lms_enrolments", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "lms_forum_threads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    cohort_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    author_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    body = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    post_count = table.Column<int>(type: "int", nullable: false),
                    last_activity_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lms_forum_threads", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "lms_published_courses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    course_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    version_no = table.Column<int>(type: "int", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    slug = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    offering_ids_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lms_published_courses", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "lms_assignment_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    enrolment_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    lesson_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    blob_key = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    content_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    submitted_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    score = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    max_score = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    rubric_json = table.Column<string>(type: "json", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    feedback = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    graded_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    graded_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    passed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lms_assignment_submissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_lms_assignment_submissions_lms_enrolments_enrolment_id",
                        column: x => x.enrolment_id,
                        principalTable: "lms_enrolments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "lms_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    enrolment_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    lesson_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    deadline_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    answers_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    score = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    max_score = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    passed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    number = table.Column<int>(type: "int", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lms_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_lms_attempts_lms_enrolments_enrolment_id",
                        column: x => x.enrolment_id,
                        principalTable: "lms_enrolments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "lms_lesson_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    enrolment_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    lesson_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    position_seconds = table.Column<int>(type: "int", nullable: false),
                    page = table.Column<int>(type: "int", nullable: false),
                    percent_complete = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    client_updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_synced_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lms_lesson_progress", x => x.id);
                    table.ForeignKey(
                        name: "fk_lms_lesson_progress_lms_enrolments_enrolment_id",
                        column: x => x.enrolment_id,
                        principalTable: "lms_enrolments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "lms_offline_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    enrolment_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    learner_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    manifest_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    key_ciphertext = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    issued_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    lesson_count = table.Column<int>(type: "int", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lms_offline_packages", x => x.id);
                    table.ForeignKey(
                        name: "fk_lms_offline_packages_lms_enrolments_enrolment_id",
                        column: x => x.enrolment_id,
                        principalTable: "lms_enrolments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "lms_forum_posts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    thread_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    author_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    body = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    flag_reason = table.Column<string>(type: "varchar(48)", maxLength: 48, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    flagged_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lms_forum_posts", x => x.id);
                    table.ForeignKey(
                        name: "fk_lms_forum_posts_lms_forum_threads_thread_id",
                        column: x => x.thread_id,
                        principalTable: "lms_forum_threads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_lms_live_sessions_cohort_id",
                table: "lms_live_sessions",
                column: "cohort_id");

            migrationBuilder.CreateIndex(
                name: "ix_lms_assignment_submissions_enrolment_id_lesson_id",
                table: "lms_assignment_submissions",
                columns: new[] { "enrolment_id", "lesson_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lms_assignment_submissions_tenant_id",
                table: "lms_assignment_submissions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lms_attempts_enrolment_id_lesson_id_number",
                table: "lms_attempts",
                columns: new[] { "enrolment_id", "lesson_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lms_attempts_tenant_id",
                table: "lms_attempts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lms_cohorts_offering_id_starts_at",
                table: "lms_cohorts",
                columns: new[] { "offering_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lms_cohorts_tenant_id",
                table: "lms_cohorts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lms_cohorts_tutor_user_id",
                table: "lms_cohorts",
                column: "tutor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_lms_enrolments_cohort_id",
                table: "lms_enrolments",
                column: "cohort_id");

            migrationBuilder.CreateIndex(
                name: "ix_lms_enrolments_course_id_status",
                table: "lms_enrolments",
                columns: new[] { "course_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_lms_enrolments_learner_id_course_id",
                table: "lms_enrolments",
                columns: new[] { "learner_id", "course_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lms_enrolments_source_key",
                table: "lms_enrolments",
                column: "source_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lms_enrolments_tenant_id",
                table: "lms_enrolments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lms_forum_posts_tenant_id",
                table: "lms_forum_posts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lms_forum_posts_thread_id",
                table: "lms_forum_posts",
                column: "thread_id");

            migrationBuilder.CreateIndex(
                name: "ix_lms_forum_threads_cohort_id_last_activity_at",
                table: "lms_forum_threads",
                columns: new[] { "cohort_id", "last_activity_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lms_forum_threads_tenant_id",
                table: "lms_forum_threads",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lms_lesson_progress_enrolment_id_lesson_id",
                table: "lms_lesson_progress",
                columns: new[] { "enrolment_id", "lesson_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lms_lesson_progress_tenant_id",
                table: "lms_lesson_progress",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lms_offline_packages_enrolment_id_issued_at",
                table: "lms_offline_packages",
                columns: new[] { "enrolment_id", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lms_offline_packages_tenant_id",
                table: "lms_offline_packages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lms_published_courses_course_id",
                table: "lms_published_courses",
                column: "course_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lms_published_courses_tenant_id",
                table: "lms_published_courses",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lms_assignment_submissions");

            migrationBuilder.DropTable(
                name: "lms_attempts");

            migrationBuilder.DropTable(
                name: "lms_cohorts");

            migrationBuilder.DropTable(
                name: "lms_forum_posts");

            migrationBuilder.DropTable(
                name: "lms_lesson_progress");

            migrationBuilder.DropTable(
                name: "lms_offline_packages");

            migrationBuilder.DropTable(
                name: "lms_published_courses");

            migrationBuilder.DropTable(
                name: "lms_forum_threads");

            migrationBuilder.DropTable(
                name: "lms_enrolments");

            migrationBuilder.DropIndex(
                name: "ix_lms_live_sessions_cohort_id",
                table: "lms_live_sessions");
        }
    }
}
