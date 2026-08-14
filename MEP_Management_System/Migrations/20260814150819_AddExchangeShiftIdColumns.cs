using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddExchangeShiftIdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Thêm cột shift_id cho payments + refund_records (idempotent — IF NOT EXISTS)
            // Phục vụ ghi nhận thu/chi chênh lệch đổi hàng vào ca bán.
            migrationBuilder.Sql("ALTER TABLE payments ADD COLUMN IF NOT EXISTS shift_id uuid NULL;");
            migrationBuilder.Sql("ALTER TABLE refund_records ADD COLUMN IF NOT EXISTS shift_id uuid NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: không drop để tránh mất liên kết ca bán.
        }
    }
}
