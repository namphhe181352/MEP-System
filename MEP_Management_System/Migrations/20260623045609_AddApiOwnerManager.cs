using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddApiOwnerManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "branch_code",
                table: "branches",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "branches",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "branches",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "is_active",
                table: "branches",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.AddColumn<Guid>(
                name: "manager_user_id",
                table: "branches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "branches",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "branches",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "branches",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "inventory_transfer_tickets",
                columns: table => new
                {
                    transfer_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    transfer_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    from_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("inventory_transfer_tickets_pkey", x => x.transfer_id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_transfer_details",
                columns: table => new
                {
                    detail_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    transfer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("inventory_transfer_details_pkey", x => x.detail_id);
                    table.ForeignKey(
                        name: "FK_inventory_transfer_details_inventory_transfer_tickets_trans~",
                        column: x => x.transfer_id,
                        principalTable: "inventory_transfer_tickets",
                        principalColumn: "transfer_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_transfer_details_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_branches_manager_user_id",
                table: "branches",
                column: "manager_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfer_details_product_id",
                table: "inventory_transfer_details",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfer_details_transfer_id",
                table: "inventory_transfer_details",
                column: "transfer_id");

            migrationBuilder.CreateIndex(
                name: "uk_inventory_transfer_code",
                table: "inventory_transfer_tickets",
                column: "transfer_code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_branches_users_manager_user_id",
                table: "branches",
                column: "manager_user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_branches_users_manager_user_id",
                table: "branches");

            migrationBuilder.DropTable(
                name: "inventory_transfer_details");

            migrationBuilder.DropTable(
                name: "inventory_transfer_tickets");

            migrationBuilder.DropIndex(
                name: "IX_branches_manager_user_id",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "branch_code",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "city",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "manager_user_id",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "type",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "branches");
        }
    }
}
