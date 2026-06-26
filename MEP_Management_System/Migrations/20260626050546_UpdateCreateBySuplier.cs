using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCreateBySuplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "supplier_ratings_supplier_id_fkey",
                table: "supplier_ratings");

            migrationBuilder.DropColumn(
                name: "average_rating",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "rating_count",
                table: "suppliers");

            migrationBuilder.RenameColumn(
                name: "last_rated_at",
                table: "suppliers",
                newName: "updated_at");

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                table: "suppliers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_updated_by",
                table: "suppliers",
                column: "updated_by");

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_ratings_suppliers_supplier_id",
                table: "supplier_ratings",
                column: "supplier_id",
                principalTable: "suppliers",
                principalColumn: "supplier_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "suppliers_updated_by_fkey",
                table: "suppliers",
                column: "updated_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_supplier_ratings_suppliers_supplier_id",
                table: "supplier_ratings");

            migrationBuilder.DropForeignKey(
                name: "suppliers_updated_by_fkey",
                table: "suppliers");

            migrationBuilder.DropIndex(
                name: "IX_suppliers_updated_by",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "suppliers");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "suppliers",
                newName: "last_rated_at");

            migrationBuilder.AddColumn<decimal>(
                name: "average_rating",
                table: "suppliers",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rating_count",
                table: "suppliers",
                type: "integer",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "supplier_ratings_supplier_id_fkey",
                table: "supplier_ratings",
                column: "supplier_id",
                principalTable: "suppliers",
                principalColumn: "supplier_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
