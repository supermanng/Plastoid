using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Sponsorship.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeatSponsorship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sponsorship_seat_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    block_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    seat_no = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    learner_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    display_name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    contact_kind = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    contact_masked = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    invited_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    enrolled_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    enrolment_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    ledger_entry_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    payee_type = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payee_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    allocated_by = table.Column<Guid>(type: "binary(16)", nullable: false),
                    commission_kobo = table.Column<long>(type: "bigint", nullable: false),
                    commission_currency = table.Column<string>(type: "char(3)", nullable: false),
                    payee_net_kobo = table.Column<long>(type: "bigint", nullable: false),
                    payee_net_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tutor_share_kobo = table.Column<long>(type: "bigint", nullable: false),
                    tutor_share_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sponsorship_seat_allocations", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sponsorship_seat_blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    organisation_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    sponsor_name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    cohort_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    cohort_name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    course_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    course_title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    course_slug = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    offering_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    tutor_user_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    starts_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ends_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    seats = table.Column<int>(type: "int", nullable: false),
                    allocated = table.Column<int>(type: "int", nullable: false),
                    released = table.Column<int>(type: "int", nullable: false),
                    units_redeemed = table.Column<long>(type: "bigint", nullable: false),
                    unit_rate_kobo = table.Column<long>(type: "bigint", nullable: false),
                    wallet_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    purchased_by = table.Column<Guid>(type: "binary(16)", nullable: false),
                    purchased_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    closed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ledger_entry_id = table.Column<Guid>(type: "binary(16)", nullable: true),
                    funds_value_kobo = table.Column<long>(type: "bigint", nullable: false),
                    funds_value_currency = table.Column<string>(type: "char(3)", nullable: false),
                    price_per_seat_kobo = table.Column<long>(type: "bigint", nullable: false),
                    price_per_seat_currency = table.Column<string>(type: "char(3)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sponsorship_seat_blocks", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seat_allocations_block_id_learner_id",
                table: "sponsorship_seat_allocations",
                columns: new[] { "block_id", "learner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seat_allocations_block_id_seat_no",
                table: "sponsorship_seat_allocations",
                columns: new[] { "block_id", "seat_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seat_allocations_email",
                table: "sponsorship_seat_allocations",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seat_allocations_enrolment_id",
                table: "sponsorship_seat_allocations",
                column: "enrolment_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seat_allocations_phone_hash",
                table: "sponsorship_seat_allocations",
                column: "phone_hash");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seat_allocations_tenant_id",
                table: "sponsorship_seat_allocations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seat_blocks_cohort_id",
                table: "sponsorship_seat_blocks",
                column: "cohort_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seat_blocks_organisation_id_status",
                table: "sponsorship_seat_blocks",
                columns: new[] { "organisation_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seat_blocks_status_ends_at",
                table: "sponsorship_seat_blocks",
                columns: new[] { "status", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sponsorship_seat_blocks_tenant_id",
                table: "sponsorship_seat_blocks",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sponsorship_seat_allocations");

            migrationBuilder.DropTable(
                name: "sponsorship_seat_blocks");
        }
    }
}
