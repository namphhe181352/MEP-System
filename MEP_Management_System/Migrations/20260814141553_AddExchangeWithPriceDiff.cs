using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddExchangeWithPriceDiff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "delta_amount",
                table: "return_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "pay_amount",
                table: "return_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "return_orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "refund_amount_customer",
                table: "return_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "returned_quantity",
                table: "invoice_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "shift_id",
                table: "refund_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "shift_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exchange_items",
                columns: table => new
                {
                    exchange_item_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    return_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    conversion_rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    base_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("exchange_items_pkey", x => x.exchange_item_id);
                    table.ForeignKey(
                        name: "exchange_items_return_order_id_fkey",
                        column: x => x.return_order_id,
                        principalTable: "return_orders",
                        principalColumn: "return_order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exchange_items_branch_product_id",
                table: "exchange_items",
                column: "branch_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_exchange_items_return_order_id",
                table: "exchange_items",
                column: "return_order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exchange_items");

            migrationBuilder.DropColumn(
                name: "shift_id",
                table: "refund_records");

            migrationBuilder.DropColumn(
                name: "shift_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "delta_amount",
                table: "return_orders");

            migrationBuilder.DropColumn(
                name: "pay_amount",
                table: "return_orders");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "return_orders");

            migrationBuilder.DropColumn(
                name: "refund_amount_customer",
                table: "return_orders");

            migrationBuilder.DropColumn(
                name: "returned_quantity",
                table: "invoice_items");
        }
    }
}
