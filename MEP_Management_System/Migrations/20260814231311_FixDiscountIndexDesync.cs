using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class FixDiscountIndexDesync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Thả index cũ bị sai lệch (chỉ unique trên MinOrderValue)
            migrationBuilder.DropIndex(
                name: "ix_order_discount_tiers_min_order_value",
                table: "order_discount_tiers");

            // Đảm bảo tạo index mới (unique trên BranchId + MinOrderValue)
            // Dùng Sql để tránh lỗi nếu index đã tồn tại từ những lần EF cố gắng tạo nhưng thất bại
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS ix_order_discount_tiers_branch_min_order ON order_discount_tiers (branch_id, min_order_value);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_order_discount_tiers_branch_min_order",
                table: "order_discount_tiers");

            migrationBuilder.CreateIndex(
                name: "ix_order_discount_tiers_min_order_value",
                table: "order_discount_tiers",
                column: "min_order_value",
                unique: true);
        }
    }
}
