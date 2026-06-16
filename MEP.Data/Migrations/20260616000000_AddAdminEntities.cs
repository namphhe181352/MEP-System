using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP.Data.Migrations
{
    public partial class AddAdminEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add IsVerified to users table
            migrationBuilder.AddColumn<bool>(
                name: "is_verified",
                table: "users",
                type: "boolean",
                defaultValue: false);

            // 2. Add IsArchived and SortOrder to community_categories table
            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "community_categories",
                type: "boolean",
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "community_categories",
                type: "integer",
                defaultValue: 0);

            // 3. Create system_notifications table
            migrationBuilder.CreateTable(
                name: "system_notifications",
                columns: table => new
                {
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    target = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "DRAFT"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("system_notifications_pkey", x => x.notification_id);
                    table.ForeignKey(
                        name: "system_notifications_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            // 4. Create store_approvals table
            migrationBuilder.CreateTable(
                name: "store_approvals",
                columns: table => new
                {
                    approval_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    business_license = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tax_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    store_address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    document_urls = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    reject_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("store_approvals_pkey", x => x.approval_id);
                    table.ForeignKey(
                        name: "store_approvals_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "store_approvals_reviewed_by_fkey",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            // 5. Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_system_notifications_created_by",
                table: "system_notifications",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_store_approvals_user_id",
                table: "store_approvals",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_store_approvals_reviewed_by",
                table: "store_approvals",
                column: "reviewed_by");

            // 6. Update existing CommunityCategory seed data with SortOrder
            migrationBuilder.Sql(@"
                UPDATE community_categories SET sort_order = 1 WHERE category_name = 'Cấp nước';
                UPDATE community_categories SET sort_order = 2 WHERE category_name = 'Điện';
                UPDATE community_categories SET sort_order = 3 WHERE category_name = 'Điều hòa không khí';
                UPDATE community_categories SET sort_order = 4 WHERE category_name = 'PCCC';
                UPDATE community_categories SET sort_order = 5 WHERE category_name = 'Gas';
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "system_notifications");
            migrationBuilder.DropTable(name: "store_approvals");
            migrationBuilder.DropColumn(name: "is_verified", table: "users");
            migrationBuilder.DropColumn(name: "is_archived", table: "community_categories");
            migrationBuilder.DropColumn(name: "sort_order", table: "community_categories");
        }
    }
}
