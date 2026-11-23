using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plastoids.Modules.Sponsorship.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeatInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contact_ciphertext",
                table: "sponsorship_seat_allocations",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "invite_send_count",
                table: "sponsorship_seat_allocations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "invite_sent_at",
                table: "sponsorship_seat_allocations",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contact_ciphertext",
                table: "sponsorship_seat_allocations");

            migrationBuilder.DropColumn(
                name: "invite_send_count",
                table: "sponsorship_seat_allocations");

            migrationBuilder.DropColumn(
                name: "invite_sent_at",
                table: "sponsorship_seat_allocations");
        }
    }
}
