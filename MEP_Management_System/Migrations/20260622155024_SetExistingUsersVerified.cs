using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class SetExistingUsersVerified : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromotionCode",
                table: "orders",
                type: "text",
                nullable: true);

            // Fix: set all existing seeded staff/system accounts as verified
            migrationBuilder.Sql("UPDATE users SET is_verified = true WHERE is_verified = false OR is_verified IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "PromotionCode",
                table: "orders");
        }
    }
}
