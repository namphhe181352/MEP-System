using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimedWarrantyQty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "claimed_warranty_qty",
                table: "stock_ticket_items",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Backfill: tổng SL đã claim từ WarrantyLotAllocation (phiếu AWAITING_SUPPLIER/COMPLETED)
            // cho dữ liệu đã có trước khi thêm cột.
            migrationBuilder.Sql(@"
                UPDATE stock_ticket_items sti
                SET claimed_warranty_qty = COALESCE((
                    SELECT SUM(wla.allocate_qty)
                    FROM warranty_lot_allocations wla
                    JOIN warranty_tickets wt ON wt.warranty_ticket_id = wla.warranty_ticket_id
                    WHERE wla.ticket_item_id = sti.ticket_item_id
                      AND wt.status IN ('AWAITING_SUPPLIER','COMPLETED')
                ), 0)
                WHERE claimed_warranty_qty = 0;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ticket_items_warranty_expiry_date",
                table: "stock_ticket_items",
                column: "warranty_expiry_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stock_ticket_items_warranty_expiry_date",
                table: "stock_ticket_items");
            migrationBuilder.DropColumn(
                name: "claimed_warranty_qty",
                table: "stock_ticket_items");
        }
    }
}
