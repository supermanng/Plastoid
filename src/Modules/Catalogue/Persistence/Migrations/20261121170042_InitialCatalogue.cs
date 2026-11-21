using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Catalogue.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalogue_courses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    owner_type = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    owner_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    slug = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    summary = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    level = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    language = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    category = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    skills_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    current_version_no = table.Column<int>(type: "int", nullable: false),
                    published_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    review_note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rating_average = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    rating_count = table.Column<int>(type: "int", nullable: false),
                    estimated_minutes = table.Column<int>(type: "int", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalogue_courses", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalogue_inbox",
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
                    table.PrimaryKey("pk_catalogue_inbox", x => new { x.message_id, x.consumer });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalogue_outbox",
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
                    table.PrimaryKey("pk_catalogue_outbox", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalogue_verified_tutors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    level = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    verified_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalogue_verified_tutors", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalogue_course_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    course_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    version_no = table.Column<int>(type: "int", nullable: false),
                    outline_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    published_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    lesson_count = table.Column<int>(type: "int", nullable: false),
                    estimated_minutes = table.Column<int>(type: "int", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalogue_course_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_catalogue_course_versions_catalogue_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "catalogue_courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalogue_modules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    course_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    order = table.Column<int>(type: "int", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalogue_modules", x => x.id);
                    table.ForeignKey(
                        name: "fk_catalogue_modules_catalogue_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "catalogue_courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalogue_offerings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    course_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    format = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    funding_types_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    capacity = table.Column<int>(type: "int", nullable: true),
                    start_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    duration_weeks = table.Column<int>(type: "int", nullable: true),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    price_kobo = table.Column<long>(type: "bigint", nullable: false),
                    price_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalogue_offerings", x => x.id);
                    table.ForeignKey(
                        name: "fk_catalogue_offerings_catalogue_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "catalogue_courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalogue_review_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    course_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    automated_checks_json = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    requires_human = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    outcome = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reviewed_by = table.Column<Guid>(type: "binary(16)", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalogue_review_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_catalogue_review_tasks_catalogue_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "catalogue_courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalogue_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    course_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    learner_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    enrolment_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    rating = table.Column<int>(type: "int", nullable: false),
                    body = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    progress_at_review = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalogue_reviews", x => x.id);
                    table.ForeignKey(
                        name: "fk_catalogue_reviews_catalogue_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "catalogue_courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalogue_lessons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    course_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    module_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    type = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    order = table.Column<int>(type: "int", nullable: false),
                    duration_minutes = table.Column<int>(type: "int", nullable: false),
                    is_preview = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    media_asset_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    content_json = table.Column<string>(type: "json", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalogue_lessons", x => x.id);
                    table.ForeignKey(
                        name: "fk_catalogue_lessons_catalogue_modules_module_id",
                        column: x => x.module_id,
                        principalTable: "catalogue_modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalogue_media_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    lesson_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    provider = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    upload_id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    playback_id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    duration_seconds = table.Column<int>(type: "int", nullable: true),
                    renditions = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    failure_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalogue_media_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_catalogue_media_assets_catalogue_lessons_lesson_id",
                        column: x => x.lesson_id,
                        principalTable: "catalogue_lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_course_versions_course_id_version_no",
                table: "catalogue_course_versions",
                columns: new[] { "course_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_course_versions_tenant_id",
                table: "catalogue_course_versions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ft_catalogue_courses_title_summary",
                table: "catalogue_courses",
                columns: new[] { "title", "summary" })
                .Annotation("MySql:FullTextIndex", true);

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_courses_owner_user_id",
                table: "catalogue_courses",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_courses_slug",
                table: "catalogue_courses",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_courses_status_category",
                table: "catalogue_courses",
                columns: new[] { "status", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_courses_tenant_id",
                table: "catalogue_courses",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_lessons_course_id",
                table: "catalogue_lessons",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_lessons_module_id_order",
                table: "catalogue_lessons",
                columns: new[] { "module_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_lessons_tenant_id",
                table: "catalogue_lessons",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_media_assets_lesson_id",
                table: "catalogue_media_assets",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_media_assets_provider_upload_id",
                table: "catalogue_media_assets",
                columns: new[] { "provider", "upload_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_media_assets_tenant_id",
                table: "catalogue_media_assets",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_modules_course_id_order",
                table: "catalogue_modules",
                columns: new[] { "course_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_modules_tenant_id",
                table: "catalogue_modules",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_offerings_course_id_active",
                table: "catalogue_offerings",
                columns: new[] { "course_id", "active" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_offerings_tenant_id",
                table: "catalogue_offerings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_outbox_published_at_occurred_at",
                table: "catalogue_outbox",
                columns: new[] { "published_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_review_tasks_course_id_outcome",
                table: "catalogue_review_tasks",
                columns: new[] { "course_id", "outcome" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_review_tasks_tenant_id",
                table: "catalogue_review_tasks",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_reviews_course_id_learner_id",
                table: "catalogue_reviews",
                columns: new[] { "course_id", "learner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_reviews_tenant_id",
                table: "catalogue_reviews",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_verified_tutors_tenant_id",
                table: "catalogue_verified_tutors",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_verified_tutors_tutor_id",
                table: "catalogue_verified_tutors",
                column: "tutor_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_verified_tutors_user_id",
                table: "catalogue_verified_tutors",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalogue_course_versions");

            migrationBuilder.DropTable(
                name: "catalogue_inbox");

            migrationBuilder.DropTable(
                name: "catalogue_media_assets");

            migrationBuilder.DropTable(
                name: "catalogue_offerings");

            migrationBuilder.DropTable(
                name: "catalogue_outbox");

            migrationBuilder.DropTable(
                name: "catalogue_review_tasks");

            migrationBuilder.DropTable(
                name: "catalogue_reviews");

            migrationBuilder.DropTable(
                name: "catalogue_verified_tutors");

            migrationBuilder.DropTable(
                name: "catalogue_lessons");

            migrationBuilder.DropTable(
                name: "catalogue_modules");

            migrationBuilder.DropTable(
                name: "catalogue_courses");
        }
    }
}
