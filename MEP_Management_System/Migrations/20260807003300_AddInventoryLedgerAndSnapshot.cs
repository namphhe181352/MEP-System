using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryLedgerAndSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cost_price",
                table: "invoice_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "daily_stock_snapshots",
                columns: table => new
                {
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    closing_quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    cost_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    closing_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("daily_stock_snapshots_pkey", x => x.snapshot_id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_ledger",
                columns: table => new
                {
                    ledger_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    transaction_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    qty_change = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    unit_cost_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_cost_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    stock_after = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    avg_cost_after = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("inventory_ledger_pkey", x => x.ledger_id);
                });

            migrationBuilder.CreateIndex(
                name: "uk_daily_snapshot",
                table: "daily_stock_snapshots",
                columns: new[] { "snapshot_date", "branch_id", "branch_product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ledger_branch_product_time",
                table: "inventory_ledger",
                columns: new[] { "branch_id", "branch_product_id", "transaction_time" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_branch_time",
                table: "inventory_ledger",
                columns: new[] { "branch_id", "transaction_time" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_stock_snapshots");

            migrationBuilder.DropTable(
                name: "inventory_ledger");

            migrationBuilder.DropColumn(
                name: "cost_price",
                table: "invoice_items");
        }
    }
}
