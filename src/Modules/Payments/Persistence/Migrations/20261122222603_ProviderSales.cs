using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Payments.Persistence.Migrations
{
    /// <summary>ADR 0019: a sale of an organisation-owned course carries the payee type, the organisation, the engaged tutor's share and the engagement. Existing intents are tutor sales.</summary>
    public partial class ProviderSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "engagement_id",
                table: "payments_intents",
                type: "binary(16)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "payee_organisation_id",
                table: "payments_intents",
                type: "binary(16)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payee_type",
                table: "payments_intents",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "tutor")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "tutor_share_currency",
                table: "payments_intents",
                type: "char(3)",
                nullable: false,
                defaultValue: "NGN");

            migrationBuilder.AddColumn<long>(
                name: "tutor_share_kobo",
                table: "payments_intents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "ix_payments_intents_payee_organisation_id",
                table: "payments_intents",
                column: "payee_organisation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payments_intents_payee_organisation_id",
                table: "payments_intents");

            migrationBuilder.DropColumn(
                name: "engagement_id",
                table: "payments_intents");

            migrationBuilder.DropColumn(
                name: "payee_organisation_id",
                table: "payments_intents");

            migrationBuilder.DropColumn(
                name: "payee_type",
                table: "payments_intents");

            migrationBuilder.DropColumn(
                name: "tutor_share_currency",
                table: "payments_intents");

            migrationBuilder.DropColumn(
                name: "tutor_share_kobo",
                table: "payments_intents");
        }
    }
}
