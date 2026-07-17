using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddProductLocationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_locations",
                columns: table => new
                {
                    location_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    location_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    location_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("product_locations_pkey", x => x.location_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_locations_location_name",
                table: "product_locations",
                column: "location_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_locations");
        }
    }
}
