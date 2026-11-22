using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Payouts.Persistence.Migrations
{
    /// <summary>ADR 0019: earnings, payouts and holds carry a payee type (tutor | organisation) and a payee id (tutor_user_id renamed); organisations record a payout account. Existing rows are tutors.</summary>
    public partial class OrganisationPayees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payouts_payouts_tutor_user_id_status",
                table: "payouts_payouts");

            migrationBuilder.DropIndex(
                name: "ix_payouts_holds_tutor_user_id_released_at",
                table: "payouts_holds");

            migrationBuilder.DropIndex(
                name: "ix_payouts_earnings_tutor_user_id_status",
                table: "payouts_earnings");

            migrationBuilder.RenameColumn(
                name: "tutor_user_id",
                table: "payouts_payouts",
                newName: "payee_id");

            migrationBuilder.RenameColumn(
                name: "tutor_user_id",
                table: "payouts_holds",
                newName: "payee_id");

            migrationBuilder.RenameColumn(
                name: "tutor_user_id",
                table: "payouts_earnings",
                newName: "payee_id");

            migrationBuilder.AddColumn<string>(
                name: "payee_type",
                table: "payouts_payouts",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "tutor")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "payee_type",
                table: "payouts_holds",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "tutor")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "payee_type",
                table: "payouts_earnings",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "tutor")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payouts_organisation_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    organisation_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    bank_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    account_number_ciphertext = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    account_number_masked = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
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
                    table.PrimaryKey("pk_payouts_organisation_accounts", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_payouts_payee_type_payee_id_status",
                table: "payouts_payouts",
                columns: new[] { "payee_type", "payee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_payouts_holds_payee_type_payee_id_released_at",
                table: "payouts_holds",
                columns: new[] { "payee_type", "payee_id", "released_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payouts_earnings_payee_type_payee_id_status",
                table: "payouts_earnings",
                columns: new[] { "payee_type", "payee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_payouts_organisation_accounts_organisation_id",
                table: "payouts_organisation_accounts",
                column: "organisation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payouts_organisation_accounts_tenant_id",
                table: "payouts_organisation_accounts",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payouts_organisation_accounts");

            migrationBuilder.DropIndex(
                name: "ix_payouts_payouts_payee_type_payee_id_status",
                table: "payouts_payouts");

            migrationBuilder.DropIndex(
                name: "ix_payouts_holds_payee_type_payee_id_released_at",
                table: "payouts_holds");

            migrationBuilder.DropIndex(
                name: "ix_payouts_earnings_payee_type_payee_id_status",
                table: "payouts_earnings");

            migrationBuilder.DropColumn(
                name: "payee_type",
                table: "payouts_payouts");

            migrationBuilder.DropColumn(
                name: "payee_type",
                table: "payouts_holds");

            migrationBuilder.DropColumn(
                name: "payee_type",
                table: "payouts_earnings");

            migrationBuilder.RenameColumn(
                name: "payee_id",
                table: "payouts_payouts",
                newName: "tutor_user_id");

            migrationBuilder.RenameColumn(
                name: "payee_id",
                table: "payouts_holds",
                newName: "tutor_user_id");

            migrationBuilder.RenameColumn(
                name: "payee_id",
                table: "payouts_earnings",
                newName: "tutor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_payouts_tutor_user_id_status",
                table: "payouts_payouts",
                columns: new[] { "tutor_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_payouts_holds_tutor_user_id_released_at",
                table: "payouts_holds",
                columns: new[] { "tutor_user_id", "released_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payouts_earnings_tutor_user_id_status",
                table: "payouts_earnings",
                columns: new[] { "tutor_user_id", "status" });
        }
    }
}
