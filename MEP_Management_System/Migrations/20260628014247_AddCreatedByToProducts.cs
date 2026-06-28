using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByToProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE products ADD COLUMN IF NOT EXISTS created_by uuid;");
            migrationBuilder.Sql(@"ALTER TABLE products ADD COLUMN IF NOT EXISTS updated_by uuid;");

            migrationBuilder.CreateIndex(
                name: "IX_products_created_by",
                table: "products",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_products_updated_by",
                table: "products",
                column: "updated_by");

            migrationBuilder.AddForeignKey(
                name: "products_created_by_fkey",
                table: "products",
                column: "created_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "products_updated_by_fkey",
                table: "products",
                column: "updated_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "products_created_by_fkey",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "products_updated_by_fkey",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_created_by",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_updated_by",
                table: "products");
        }
    }
}