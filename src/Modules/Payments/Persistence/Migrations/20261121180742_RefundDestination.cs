using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Payments.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefundDestination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "destination",
                table: "payments_refunds",
                type: "varchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "learner_id",
                table: "payments_refunds",
                type: "binary(16)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "policy_driven",
                table: "payments_refunds",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "destination",
                table: "payments_refunds");

            migrationBuilder.DropColumn(
                name: "learner_id",
                table: "payments_refunds");

            migrationBuilder.DropColumn(
                name: "policy_driven",
                table: "payments_refunds");
        }
    }
}
