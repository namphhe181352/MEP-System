using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    public partial class AddAdminFields_v2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // User: Status, SuspendedUntil, SubscriptionPlan
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subscription_plan",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "suspended_until",
                table: "users",
                type: "timestamp without time zone",
                nullable: true);

            // SystemNotification: IsUrgent
            migrationBuilder.AddColumn<bool>(
                name: "is_urgent",
                table: "system_notifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // SystemLog: Level, Source
            migrationBuilder.AddColumn<string>(
                name: "level",
                table: "system_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "system_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // CommunityCategory: Slug, ParentId
            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "community_categories",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_categories_slug",
                table: "community_categories",
                column: "slug",
                unique: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_id",
                table: "community_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_categories_parent_id",
                table: "community_categories",
                column: "parent_id");

            migrationBuilder.AddForeignKey(
                name: "community_categories_parent_id_fkey",
                table: "community_categories",
                column: "parent_id",
                principalTable: "community_categories",
                principalColumn: "category_id",
                onDelete: ReferentialAction.SetNull);

            // Update existing seed slugs for existing categories
            migrationBuilder.Sql(@"
                UPDATE community_categories SET slug = 'cap-nuoc' WHERE category_name = 'Cấp nước';
                UPDATE community_categories SET slug = 'dien' WHERE category_name = 'Điện';
                UPDATE community_categories SET slug = 'dieu-hoa-khong-khi' WHERE category_name = 'Điều hòa không khí';
                UPDATE community_categories SET slug = 'pccc' WHERE category_name = 'PCCC';
                UPDATE community_categories SET slug = 'gas' WHERE category_name = 'Gas';
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "community_categories_parent_id_fkey",
                table: "community_categories");

            migrationBuilder.DropIndex(
                name: "ix_community_categories_slug",
                table: "community_categories");

            migrationBuilder.DropIndex(
                name: "ix_community_categories_parent_id",
                table: "community_categories");

            migrationBuilder.DropColumn(name: "status", table: "users");
            migrationBuilder.DropColumn(name: "subscription_plan", table: "users");
            migrationBuilder.DropColumn(name: "suspended_until", table: "users");
            migrationBuilder.DropColumn(name: "is_urgent", table: "system_notifications");
            migrationBuilder.DropColumn(name: "level", table: "system_logs");
            migrationBuilder.DropColumn(name: "source", table: "system_logs");
            migrationBuilder.DropColumn(name: "slug", table: "community_categories");
            migrationBuilder.DropColumn(name: "parent_id", table: "community_categories");
        }
    }
}
