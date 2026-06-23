using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_created_by",
                table: "users",
                column: "created_by");

            migrationBuilder.AddForeignKey(
                name: "users_created_by_fkey",
                table: "users",
                column: "created_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "users_created_by_fkey",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_created_by",
                table: "users");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "users");
        }
    }
}
