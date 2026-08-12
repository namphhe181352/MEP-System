using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class BackfillUnitNameForExistingItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill UnitName for existing stock_ticket_items
            migrationBuilder.Sql(@"
                UPDATE stock_ticket_items sti
                SET ""UnitName"" = p.base_unit
                FROM branch_products bp
                INNER JOIN products p ON bp.product_id = p.product_id
                WHERE sti.branch_product_id = bp.branch_product_id
                  AND sti.""UnitName"" IS NULL;
            ");

            // Backfill UnitName for existing invoice_items
            migrationBuilder.Sql(@"
                UPDATE invoice_items ii
                SET ""UnitName"" = p.base_unit
                FROM branch_products bp
                INNER JOIN products p ON bp.product_id = p.product_id
                WHERE ii.branch_product_id = bp.branch_product_id
                  AND ii.""UnitName"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cannot restore NULL values - no-op
        }
    }
}
