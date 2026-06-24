using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByToSupplierRelated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "suppliers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "SupplierPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "supplier_ratings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_created_by",
                table: "suppliers",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_created_by",
                table: "SupplierPayments",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_ratings_created_by",
                table: "supplier_ratings",
                column: "created_by");

            migrationBuilder.AddForeignKey(
                name: "supplier_ratings_created_by_fkey",
                table: "supplier_ratings",
                column: "created_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "supplier_payments_created_by_fkey",
                table: "SupplierPayments",
                column: "created_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "suppliers_created_by_fkey",
                table: "suppliers",
                column: "created_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "supplier_ratings_created_by_fkey",
                table: "supplier_ratings");

            migrationBuilder.DropForeignKey(
                name: "supplier_payments_created_by_fkey",
                table: "SupplierPayments");

            migrationBuilder.DropForeignKey(
                name: "suppliers_created_by_fkey",
                table: "suppliers");

            migrationBuilder.DropIndex(
                name: "IX_suppliers_created_by",
                table: "suppliers");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPayments_created_by",
                table: "SupplierPayments");

            migrationBuilder.DropIndex(
                name: "IX_supplier_ratings_created_by",
                table: "supplier_ratings");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "supplier_ratings");
        }
    }
}
