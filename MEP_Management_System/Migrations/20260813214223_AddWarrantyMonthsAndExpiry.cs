using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddWarrantyMonthsAndExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "warranty_expiry_date",
                table: "stock_ticket_items",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "warranty_months",
                table: "stock_ticket_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "default_warranty_months",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 12);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "warranty_expiry_date",
                table: "stock_ticket_items");

            migrationBuilder.DropColumn(
                name: "warranty_months",
                table: "stock_ticket_items");

            migrationBuilder.DropColumn(
                name: "default_warranty_months",
                table: "products");
        }
    }
}
