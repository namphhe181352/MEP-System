using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchIdToDiscountTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                table: "order_discount_tiers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_discount_tiers_branch_id",
                table: "order_discount_tiers",
                column: "branch_id");

            migrationBuilder.AddForeignKey(
                name: "FK_order_discount_tiers_branches_branch_id",
                table: "order_discount_tiers",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "branch_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_discount_tiers_branches_branch_id",
                table: "order_discount_tiers");

            migrationBuilder.DropIndex(
                name: "IX_order_discount_tiers_branch_id",
                table: "order_discount_tiers");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "order_discount_tiers");
        }
    }
}
