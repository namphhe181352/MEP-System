using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    public partial class AddSupplierRating : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===== Thêm cột mới vào bảng suppliers =====
            migrationBuilder.AddColumn<string>(
                name: "contact_person",
                table: "suppliers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_code",
                table: "suppliers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_name",
                table: "suppliers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bank_account",
                table: "suppliers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bank_name",
                table: "suppliers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "website",
                table: "suppliers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "average_rating",
                table: "suppliers",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rating_count",
                table: "suppliers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_rated_at",
                table: "suppliers",
                type: "timestamp without time zone",
                nullable: true);

            // ===== Tạo bảng supplier_ratings =====
            migrationBuilder.CreateTable(
                name: "supplier_ratings",
                columns: table => new
                {
                    rating_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quality_score = table.Column<short>(type: "smallint", nullable: false),
                    delivery_score = table.Column<short>(type: "smallint", nullable: false),
                    price_score = table.Column<short>(type: "smallint", nullable: false),
                    service_score = table.Column<short>(type: "smallint", nullable: false),
                    overall_rating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    rated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("supplier_ratings_pkey", x => x.rating_id);
                    table.ForeignKey(
                        name: "supplier_ratings_supplier_id_fkey",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "supplier_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "supplier_ratings_po_id_fkey",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "purchase_order_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "supplier_ratings_rated_by_fkey",
                        column: x => x.rated_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_ratings_supplier_id",
                table: "supplier_ratings",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_ratings_rated_at",
                table: "supplier_ratings",
                column: "rated_at");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "supplier_ratings");

            migrationBuilder.DropColumn(name: "contact_person", table: "suppliers");
            migrationBuilder.DropColumn(name: "tax_code", table: "suppliers");
            migrationBuilder.DropColumn(name: "group_name", table: "suppliers");
            migrationBuilder.DropColumn(name: "bank_account", table: "suppliers");
            migrationBuilder.DropColumn(name: "bank_name", table: "suppliers");
            migrationBuilder.DropColumn(name: "website", table: "suppliers");
            migrationBuilder.DropColumn(name: "notes", table: "suppliers");
            migrationBuilder.DropColumn(name: "average_rating", table: "suppliers");
            migrationBuilder.DropColumn(name: "rating_count", table: "suppliers");
            migrationBuilder.DropColumn(name: "last_rated_at", table: "suppliers");
        }
    }
}
