using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Lms.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeatSponsorship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "funding_label",
                table: "lms_enrolments",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "reserved",
                table: "lms_cohorts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "funding_label",
                table: "lms_enrolments");

            migrationBuilder.DropColumn(
                name: "reserved",
                table: "lms_cohorts");
        }
    }
}
