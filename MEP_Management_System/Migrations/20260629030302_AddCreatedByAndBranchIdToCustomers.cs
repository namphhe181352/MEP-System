using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByAndBranchIdToCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserUserId",
                table: "customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_BranchId",
                table: "customers",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_customers_CreatedByUserUserId",
                table: "customers",
                column: "CreatedByUserUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_customers_branches_BranchId",
                table: "customers",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "branch_id");

            migrationBuilder.AddForeignKey(
                name: "FK_customers_users_CreatedByUserUserId",
                table: "customers",
                column: "CreatedByUserUserId",
                principalTable: "users",
                principalColumn: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_customers_branches_BranchId",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "FK_customers_users_CreatedByUserUserId",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_BranchId",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_CreatedByUserUserId",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "CreatedByUserUserId",
                table: "customers");
        }
    }
}
