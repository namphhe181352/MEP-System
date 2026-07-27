using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class ExpenseCategoryHardDelete_20260727 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "expense_vouchers_category_id_fkey",
                table: "expense_vouchers");

            migrationBuilder.AlterColumn<Guid>(
                name: "category_id",
                table: "expense_vouchers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "expense_vouchers_category_id_fkey",
                table: "expense_vouchers",
                column: "category_id",
                principalTable: "expense_categories",
                principalColumn: "category_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "expense_vouchers_category_id_fkey",
                table: "expense_vouchers");

            migrationBuilder.AlterColumn<Guid>(
                name: "category_id",
                table: "expense_vouchers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "expense_vouchers_category_id_fkey",
                table: "expense_vouchers",
                column: "category_id",
                principalTable: "expense_categories",
                principalColumn: "category_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
