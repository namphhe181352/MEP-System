using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddAssigneeToStockTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "products_product_code_key",
                table: "products");

            migrationBuilder.AddColumn<Guid>(
                name: "assignee_user_id",
                table: "stock_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_tickets_assignee_user_id",
                table: "stock_tickets",
                column: "assignee_user_id");

            migrationBuilder.CreateIndex(
                name: "products_product_code_branch_id_key",
                table: "products",
                columns: new[] { "product_code", "BranchId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "stock_tickets_assignee_user_id_fkey",
                table: "stock_tickets",
                column: "assignee_user_id",
                principalTable: "users",
                principalColumn: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "stock_tickets_assignee_user_id_fkey",
                table: "stock_tickets");

            migrationBuilder.DropIndex(
                name: "IX_stock_tickets_assignee_user_id",
                table: "stock_tickets");

            migrationBuilder.DropIndex(
                name: "products_product_code_branch_id_key",
                table: "products");

            migrationBuilder.DropColumn(
                name: "assignee_user_id",
                table: "stock_tickets");

            migrationBuilder.CreateIndex(
                name: "products_product_code_key",
                table: "products",
                column: "product_code",
                unique: true);
        }
    }
}
