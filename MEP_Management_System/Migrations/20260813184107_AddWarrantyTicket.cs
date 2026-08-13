using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddWarrantyTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "warranty_quantity",
                table: "branch_products",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "warranty_tickets",
                columns: table => new
                {
                    warranty_ticket_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    return_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    completed_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("warranty_tickets_pkey", x => x.warranty_ticket_id);
                    table.ForeignKey(
                        name: "warranty_tickets_return_item_id_fkey",
                        column: x => x.return_item_id,
                        principalTable: "return_items",
                        principalColumn: "return_item_id");
                    table.ForeignKey(
                        name: "warranty_tickets_supplier_id_fkey",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "supplier_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_warranty_tickets_return_item_id",
                table: "warranty_tickets",
                column: "return_item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warranty_tickets_supplier_id",
                table: "warranty_tickets",
                column: "supplier_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "warranty_tickets");

            migrationBuilder.DropColumn(
                name: "warranty_quantity",
                table: "branch_products");
        }
    }
}
