using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchIdToTaxonomyAndProductTaxonomyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // UnitPrice column already exists, skipping
            migrationBuilder.AddColumn<Guid>(
                name: "BrandTaxonomyId",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryTaxonomyId",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                table: "product_taxonomies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "media_url",
                table: "media_resources",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateIndex(
                name: "IX_products_BrandTaxonomyId",
                table: "products",
                column: "BrandTaxonomyId");

            migrationBuilder.CreateIndex(
                name: "IX_products_CategoryTaxonomyId",
                table: "products",
                column: "CategoryTaxonomyId");

            migrationBuilder.CreateIndex(
                name: "IX_product_taxonomies_branch_id",
                table: "product_taxonomies",
                column: "branch_id");

            migrationBuilder.AddForeignKey(
                name: "FK_product_taxonomies_branches_branch_id",
                table: "product_taxonomies",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "branch_id");

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_taxonomies_BrandTaxonomyId",
                table: "products",
                column: "BrandTaxonomyId",
                principalTable: "product_taxonomies",
                principalColumn: "taxonomy_id");

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_taxonomies_CategoryTaxonomyId",
                table: "products",
                column: "CategoryTaxonomyId",
                principalTable: "product_taxonomies",
                principalColumn: "taxonomy_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_product_taxonomies_branches_branch_id",
                table: "product_taxonomies");

            migrationBuilder.DropForeignKey(
                name: "FK_products_product_taxonomies_BrandTaxonomyId",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_product_taxonomies_CategoryTaxonomyId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_BrandTaxonomyId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_CategoryTaxonomyId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_product_taxonomies_branch_id",
                table: "product_taxonomies");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "stock_ticket_items");

            migrationBuilder.DropColumn(
                name: "BrandTaxonomyId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CategoryTaxonomyId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "product_taxonomies");

            migrationBuilder.AlterColumn<string>(
                name: "media_url",
                table: "media_resources",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
