using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchIdToSupplierAndExpenseCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "suppliers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "expense_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_BranchId",
                table: "suppliers",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_expense_categories_BranchId",
                table: "expense_categories",
                column: "BranchId");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropIndex(
                name: "IX_suppliers_BranchId",
                table: "suppliers");

            migrationBuilder.DropIndex(
                name: "IX_expense_categories_BranchId",
                table: "expense_categories");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "expense_categories");
        }
    }
}
