using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AlignInventoryCheckNotificationsAndRecountMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "recount_number",
                table: "inventory_check_tickets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "recount_reason",
                table: "inventory_check_tickets",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "recount_requested_at",
                table: "inventory_check_tickets",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recount_requested_by_user_id",
                table: "inventory_check_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_check_tickets_recount_requested_by_user_id",
                table: "inventory_check_tickets",
                column: "recount_requested_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "inventory_check_tickets_recount_requested_by_user_id_fkey",
                table: "inventory_check_tickets",
                column: "recount_requested_by_user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "inventory_check_tickets_recount_requested_by_user_id_fkey",
                table: "inventory_check_tickets");

            migrationBuilder.DropIndex(
                name: "IX_inventory_check_tickets_recount_requested_by_user_id",
                table: "inventory_check_tickets");

            migrationBuilder.DropColumn(
                name: "recount_number",
                table: "inventory_check_tickets");

            migrationBuilder.DropColumn(
                name: "recount_reason",
                table: "inventory_check_tickets");

            migrationBuilder.DropColumn(
                name: "recount_requested_at",
                table: "inventory_check_tickets");

            migrationBuilder.DropColumn(
                name: "recount_requested_by_user_id",
                table: "inventory_check_tickets");
        }
    }
}
