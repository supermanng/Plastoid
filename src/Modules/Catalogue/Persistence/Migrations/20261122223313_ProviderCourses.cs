using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Catalogue.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProviderCourses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "engagement_id",
                table: "catalogue_offerings",
                type: "binary(16)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tutor_user_id",
                table: "catalogue_offerings",
                type: "binary(16)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "organisation_id",
                table: "catalogue_courses",
                type: "binary(16)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "catalogue_engagements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    course_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    organisation_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    terms_kind = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    share_bps = table.Column<int>(type: "int", nullable: false),
                    fixed_fee_kobo = table.Column<long>(type: "bigint", nullable: true),
                    fixed_fee_currency = table.Column<string>(type: "char(3)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    offered_by = table.Column<Guid>(type: "binary(16)", nullable: false),
                    offered_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    responded_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ended_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    end_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalogue_engagements", x => x.id);
                    table.ForeignKey(
                        name: "fk_catalogue_engagements_catalogue_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "catalogue_courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_offerings_tutor_user_id",
                table: "catalogue_offerings",
                column: "tutor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_courses_organisation_id",
                table: "catalogue_courses",
                column: "organisation_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_engagements_course_id_tutor_user_id_status",
                table: "catalogue_engagements",
                columns: new[] { "course_id", "tutor_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_engagements_organisation_id_status",
                table: "catalogue_engagements",
                columns: new[] { "organisation_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_engagements_tenant_id",
                table: "catalogue_engagements",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_engagements_tutor_user_id_status",
                table: "catalogue_engagements",
                columns: new[] { "tutor_user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalogue_engagements");

            migrationBuilder.DropIndex(
                name: "ix_catalogue_offerings_tutor_user_id",
                table: "catalogue_offerings");

            migrationBuilder.DropIndex(
                name: "ix_catalogue_courses_organisation_id",
                table: "catalogue_courses");

            migrationBuilder.DropColumn(
                name: "engagement_id",
                table: "catalogue_offerings");

            migrationBuilder.DropColumn(
                name: "tutor_user_id",
                table: "catalogue_offerings");

            migrationBuilder.DropColumn(
                name: "organisation_id",
                table: "catalogue_courses");
        }
    }
}
