using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierRatingColumns : Migration
    {
        /// <summary>
        /// UP đã được làm rỗng (no-op) vì migration 20260621000000_AddSupplierRating
        /// đã apply đầy đủ các thay đổi (10 cột suppliers + bảng supplier_ratings).
        /// Migration này được giữ lại để khớp với __EFMigrationsHistory đã ghi,
        /// tránh EF phát hiện model snapshot lệch DB.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: xem giải thích ở XML comment trên class.
        }

        /// <summary>
        /// DOWN đã được làm rỗng (no-op) vì lý do ngược lại với Up().
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op.
        }
    }
}

