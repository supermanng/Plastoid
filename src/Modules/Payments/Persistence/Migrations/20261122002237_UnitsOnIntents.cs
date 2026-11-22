using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Payments.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnitsOnIntents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "payments_intents",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "unit_rate_kobo",
                table: "payments_intents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "units_charged",
                table: "payments_intents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "units_value_kobo",
                table: "payments_intents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "units_wallet_id",
                table: "payments_intents",
                type: "binary(16)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "payments_intents");

            migrationBuilder.DropColumn(
                name: "unit_rate_kobo",
                table: "payments_intents");

            migrationBuilder.DropColumn(
                name: "units_charged",
                table: "payments_intents");

            migrationBuilder.DropColumn(
                name: "units_value_kobo",
                table: "payments_intents");

            migrationBuilder.DropColumn(
                name: "units_wallet_id",
                table: "payments_intents");
        }
    }
}
