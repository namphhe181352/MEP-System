using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierDebtLinkToStockTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                table: "stock_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_amount",
                table: "stock_tickets",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_stock_tickets_supplier_id",
                table: "stock_tickets",
                column: "supplier_id");

            migrationBuilder.AddForeignKey(
                name: "stock_tickets_supplier_id_fkey",
                table: "stock_tickets",
                column: "supplier_id",
                principalTable: "suppliers",
                principalColumn: "supplier_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "stock_tickets_supplier_id_fkey",
                table: "stock_tickets");

            migrationBuilder.DropIndex(
                name: "IX_stock_tickets_supplier_id",
                table: "stock_tickets");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                table: "stock_tickets");

            migrationBuilder.DropColumn(
                name: "total_amount",
                table: "stock_tickets");
        }
    }
}
