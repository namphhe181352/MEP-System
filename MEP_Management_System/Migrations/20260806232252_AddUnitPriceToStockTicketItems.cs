using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitPriceToStockTicketItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UnitPrice",
                table: "stock_ticket_items",
                newName: "unit_price");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                table: "stock_ticket_items",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "unit_price",
                table: "stock_ticket_items",
                newName: "UnitPrice");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "stock_ticket_items",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(15,2)",
                oldPrecision: 15,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
