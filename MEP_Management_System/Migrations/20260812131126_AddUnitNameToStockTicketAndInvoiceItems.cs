using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitNameToStockTicketAndInvoiceItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnitName",
                table: "stock_ticket_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitName",
                table: "invoice_items",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitName",
                table: "stock_ticket_items");

            migrationBuilder.DropColumn(
                name: "UnitName",
                table: "invoice_items");
        }
    }
}
