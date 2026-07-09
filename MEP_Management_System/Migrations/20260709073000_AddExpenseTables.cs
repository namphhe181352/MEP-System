using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expense_categories",
                columns: table => new
                {
                    category_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    category_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("expense_categories_pkey", x => x.category_id);
                });

            migrationBuilder.CreateIndex(
                name: "expense_categories_category_name_key",
                table: "expense_categories",
                column: "category_name",
                unique: true);

            migrationBuilder.CreateTable(
                name: "expense_vouchers",
                columns: table => new
                {
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    voucher_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("expense_vouchers_pkey", x => x.voucher_id);
                    table.ForeignKey(
                        name: "expense_vouchers_branch_id_fkey",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "branch_id");
                    table.ForeignKey(
                        name: "expense_vouchers_category_id_fkey",
                        column: x => x.category_id,
                        principalTable: "expense_categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "expense_vouchers_voucher_code_key",
                table: "expense_vouchers",
                column: "voucher_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_vouchers_status",
                table: "expense_vouchers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_expense_vouchers_category_id",
                table: "expense_vouchers",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_vouchers_branch_id",
                table: "expense_vouchers",
                column: "branch_id");

            migrationBuilder.InsertData(
                table: "expense_categories",
                columns: new[] { "category_id", "category_name", "is_active" },
                values: new object[,]
                {
                    { Guid.Parse("11111111-1111-1111-1111-111111111111"), "Điện nước", (short)1 },
                    { Guid.Parse("22222222-2222-2222-2222-222222222222"), "Lương & Phụ cấp", (short)1 },
                    { Guid.Parse("33333333-3333-3333-3333-333333333333"), "Thuê mặt bằng", (short)1 },
                    { Guid.Parse("44444444-4444-4444-4444-444444444444"), "Vận chuyển", (short)1 },
                    { Guid.Parse("55555555-5555-5555-5555-555555555555"), "Văn phòng phẩm", (short)1 },
                    { Guid.Parse("66666666-6666-6666-6666-666666666666"), "Bảo trì & Sửa chữa", (short)1 },
                    { Guid.Parse("77777777-7777-7777-7777-777777777777"), "Marketing & Quảng cáo", (short)1 },
                    { Guid.Parse("88888888-8888-8888-8888-888888888888"), "Chi phí khác", (short)1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "expense_vouchers");
            migrationBuilder.DropTable(name: "expense_categories");
        }
    }
}
