using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOutWardController : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancel_reason",
                table: "stock_tickets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                table: "stock_tickets",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by",
                table: "stock_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "stock_tickets",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "confirmed_by",
                table: "stock_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_tickets_cancelled_by",
                table: "stock_tickets",
                column: "cancelled_by");

            migrationBuilder.CreateIndex(
                name: "IX_stock_tickets_confirmed_by",
                table: "stock_tickets",
                column: "confirmed_by");

            migrationBuilder.AddForeignKey(
                name: "stock_tickets_cancelled_by_fkey",
                table: "stock_tickets",
                column: "cancelled_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "stock_tickets_confirmed_by_fkey",
                table: "stock_tickets",
                column: "confirmed_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "stock_tickets_cancelled_by_fkey",
                table: "stock_tickets");

            migrationBuilder.DropForeignKey(
                name: "stock_tickets_confirmed_by_fkey",
                table: "stock_tickets");

            migrationBuilder.DropIndex(
                name: "IX_stock_tickets_cancelled_by",
                table: "stock_tickets");

            migrationBuilder.DropIndex(
                name: "IX_stock_tickets_confirmed_by",
                table: "stock_tickets");

            migrationBuilder.DropColumn(
                name: "cancel_reason",
                table: "stock_tickets");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "stock_tickets");

            migrationBuilder.DropColumn(
                name: "cancelled_by",
                table: "stock_tickets");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "stock_tickets");

            migrationBuilder.DropColumn(
                name: "confirmed_by",
                table: "stock_tickets");
        }
    }
}
