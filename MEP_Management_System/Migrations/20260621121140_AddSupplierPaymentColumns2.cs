using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierPaymentColumns2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPayments_users_RecordedByUserUserId",
                table: "SupplierPayments");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPayments_RecordedByUserUserId",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "RecordedByUserUserId",
                table: "SupplierPayments");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_RecordedBy",
                table: "SupplierPayments",
                column: "RecordedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPayments_users_RecordedBy",
                table: "SupplierPayments",
                column: "RecordedBy",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPayments_users_RecordedBy",
                table: "SupplierPayments");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPayments_RecordedBy",
                table: "SupplierPayments");

            migrationBuilder.AddColumn<Guid>(
                name: "RecordedByUserUserId",
                table: "SupplierPayments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_RecordedByUserUserId",
                table: "SupplierPayments",
                column: "RecordedByUserUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPayments_users_RecordedByUserUserId",
                table: "SupplierPayments",
                column: "RecordedByUserUserId",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
