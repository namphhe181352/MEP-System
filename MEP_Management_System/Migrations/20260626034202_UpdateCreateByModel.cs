using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCreateByModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "updated_by",
                table: "products",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "created_by",
                table: "products",
                newName: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_products_CreatedBy",
                table: "products",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_products_UpdatedBy",
                table: "products",
                column: "UpdatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Product_User_CreatedBy",
                table: "products",
                column: "CreatedBy",
                principalTable: "users",
                principalColumn: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Product_User_UpdatedBy",
                table: "products",
                column: "UpdatedBy",
                principalTable: "users",
                principalColumn: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Product_User_CreatedBy",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_Product_User_UpdatedBy",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_CreatedBy",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_UpdatedBy",
                table: "products");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "products",
                newName: "updated_by");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "products",
                newName: "created_by");
        }
    }
}
