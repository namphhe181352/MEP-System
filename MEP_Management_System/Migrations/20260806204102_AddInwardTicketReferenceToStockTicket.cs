using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddInwardTicketReferenceToStockTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "inward_ticket_id",
                table: "stock_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "inward_ticket_item_id",
                table: "stock_ticket_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_tickets_inward_ticket_id",
                table: "stock_tickets",
                column: "inward_ticket_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ticket_items_inward_ticket_item_id",
                table: "stock_ticket_items",
                column: "inward_ticket_item_id");

            migrationBuilder.AddForeignKey(
                name: "stock_ticket_items_inward_ticket_item_id_fkey",
                table: "stock_ticket_items",
                column: "inward_ticket_item_id",
                principalTable: "stock_ticket_items",
                principalColumn: "ticket_item_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "stock_tickets_inward_ticket_id_fkey",
                table: "stock_tickets",
                column: "inward_ticket_id",
                principalTable: "stock_tickets",
                principalColumn: "stock_ticket_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "stock_ticket_items_inward_ticket_item_id_fkey",
                table: "stock_ticket_items");

            migrationBuilder.DropForeignKey(
                name: "stock_tickets_inward_ticket_id_fkey",
                table: "stock_tickets");

            migrationBuilder.DropIndex(
                name: "IX_stock_tickets_inward_ticket_id",
                table: "stock_tickets");

            migrationBuilder.DropIndex(
                name: "IX_stock_ticket_items_inward_ticket_item_id",
                table: "stock_ticket_items");

            migrationBuilder.DropColumn(
                name: "inward_ticket_id",
                table: "stock_tickets");

            migrationBuilder.DropColumn(
                name: "inward_ticket_item_id",
                table: "stock_ticket_items");
        }
    }
}
