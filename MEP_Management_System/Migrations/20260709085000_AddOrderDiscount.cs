using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Thêm các cột mới vào bảng invoices
            migrationBuilder.AddColumn<decimal>(
                name: "sub_total_amount",
                table: "invoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_percent",
                table: "invoices",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_amount",
                table: "invoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Tạo bảng order_discount_tiers
            migrationBuilder.CreateTable(
                name: "order_discount_tiers",
                columns: table => new
                {
                    order_discount_tier_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    min_order_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    discount_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("order_discount_tiers_pkey", x => x.order_discount_tier_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_discount_tiers_min_order_value",
                table: "order_discount_tiers",
                column: "min_order_value",
                unique: true);

            // Thêm dữ liệu mẫu
            migrationBuilder.InsertData(
                table: "order_discount_tiers",
                columns: new[] { "min_order_value", "discount_percent", "is_active" },
                values: new object[,]
                {
                    { 5000000m, 3m, true },
                    { 10000000m, 5m, true },
                    { 20000000m, 8m, true }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "order_discount_tiers");

            migrationBuilder.DropColumn(name: "sub_total_amount", table: "invoices");
            migrationBuilder.DropColumn(name: "discount_percent", table: "invoices");
            migrationBuilder.DropColumn(name: "discount_amount", table: "invoices");
        }
    }
}
