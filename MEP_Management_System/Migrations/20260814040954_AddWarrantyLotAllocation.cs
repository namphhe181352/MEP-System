using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddWarrantyLotAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "warranty_lot_allocations",
                columns: table => new
                {
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    warranty_ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocate_qty = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("warranty_lot_allocations_pkey", x => x.allocation_id);
                    table.ForeignKey(
                        name: "warranty_lot_allocations_ticket_item_id_fkey",
                        column: x => x.ticket_item_id,
                        principalTable: "stock_ticket_items",
                        principalColumn: "ticket_item_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "warranty_lot_allocations_warranty_ticket_id_fkey",
                        column: x => x.warranty_ticket_id,
                        principalTable: "warranty_tickets",
                        principalColumn: "warranty_ticket_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_warranty_lot_allocations_ticket_item_id",
                table: "warranty_lot_allocations",
                column: "ticket_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_warranty_lot_allocations_warranty_ticket_id",
                table: "warranty_lot_allocations",
                column: "warranty_ticket_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "warranty_lot_allocations");
        }
    }
}
