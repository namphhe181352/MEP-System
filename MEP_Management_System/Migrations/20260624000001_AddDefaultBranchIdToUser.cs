using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultBranchIdToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "default_branch_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_default_branch_id",
                table: "users",
                column: "default_branch_id");

            migrationBuilder.AddForeignKey(
                name: "users_default_branch_id_fkey",
                table: "users",
                column: "default_branch_id",
                principalTable: "branches",
                principalColumn: "branch_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "users_default_branch_id_fkey",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_default_branch_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "default_branch_id",
                table: "users");
        }
    }
}
