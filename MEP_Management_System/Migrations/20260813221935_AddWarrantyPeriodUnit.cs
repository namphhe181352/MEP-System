using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddWarrantyPeriodUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "warranty_months",
                table: "stock_ticket_items",
                newName: "warranty_period");

            migrationBuilder.RenameColumn(
                name: "default_warranty_months",
                table: "products",
                newName: "warranty_period");

            migrationBuilder.AddColumn<string>(
                name: "warranty_unit",
                table: "stock_ticket_items",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "MONTH");

            migrationBuilder.AddColumn<string>(
                name: "warranty_unit",
                table: "products",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "MONTH");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "warranty_unit",
                table: "stock_ticket_items");

            migrationBuilder.DropColumn(
                name: "warranty_unit",
                table: "products");

            migrationBuilder.RenameColumn(
                name: "warranty_period",
                table: "stock_ticket_items",
                newName: "warranty_months");

            migrationBuilder.RenameColumn(
                name: "warranty_period",
                table: "products",
                newName: "default_warranty_months");
        }
    }
}
