using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class FixAttributeTypeUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_product_attribute_types_type_name",
                table: "product_attribute_types");

            migrationBuilder.RenameColumn(
                name: "UnitName",
                table: "stock_ticket_items",
                newName: "unit_name");

            migrationBuilder.AddColumn<int>(
                name: "recount_number",
                table: "stock_tickets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "recount_reason",
                table: "stock_tickets",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "recount_requested_at",
                table: "stock_tickets",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recount_requested_by_user_id",
                table: "stock_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "stock_ticket_items",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "goods_issues",
                columns: table => new
                {
                    goods_issue_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    issue_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issue_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    customer_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    customer_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    issue_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sync_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sync_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_payment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("goods_issues_pkey", x => x.goods_issue_id);
                    table.ForeignKey(
                        name: "fk_goods_issues_branch",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_approvals",
                columns: table => new
                {
                    approval_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    business_license = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    tax_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    store_address = table.Column<string>(type: "text", nullable: true),
                    document_urls = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reject_reason = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("store_approvals_pkey", x => x.approval_id);
                });

            migrationBuilder.CreateTable(
                name: "WarrantyClaims",
                columns: table => new
                {
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimCode = table.Column<string>(type: "text", nullable: true),
                    ClaimDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClaimedQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    IssueDescription = table.Column<string>(type: "text", nullable: true),
                    CustomerName = table.Column<string>(type: "text", nullable: true),
                    CustomerPhone = table.Column<string>(type: "text", nullable: true),
                    ReplacementProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessNotes = table.Column<string>(type: "text", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarrantyClaims", x => x.ClaimId);
                    table.ForeignKey(
                        name: "FK_WarrantyClaims_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WarrantyClaims_stock_ticket_items_TicketItemId",
                        column: x => x.TicketItemId,
                        principalTable: "stock_ticket_items",
                        principalColumn: "ticket_item_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WarrantyClaims_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "supplier_id");
                    table.ForeignKey(
                        name: "FK_WarrantyClaims_users_ClaimedByUserId",
                        column: x => x.ClaimedByUserId,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK_WarrantyClaims_users_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "goods_issue_attachments",
                columns: table => new
                {
                    goods_issue_attachment_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    goods_issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    size = table.Column<long>(type: "bigint", nullable: true),
                    media_url = table.Column<string>(type: "text", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("goods_issue_attachments_pkey", x => x.goods_issue_attachment_id);
                    table.ForeignKey(
                        name: "fk_goods_issue_attachments_issue",
                        column: x => x.goods_issue_id,
                        principalTable: "goods_issues",
                        principalColumn: "goods_issue_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "goods_issue_items",
                columns: table => new
                {
                    goods_issue_item_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    goods_issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    product_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    product_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    lot_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    expiry_date = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    warehouse_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    warehouse_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("goods_issue_items_pkey", x => x.goods_issue_item_id);
                    table.ForeignKey(
                        name: "fk_goods_issue_items_issue",
                        column: x => x.goods_issue_id,
                        principalTable: "goods_issues",
                        principalColumn: "goods_issue_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_tickets_recount_requested_by_user_id",
                table: "stock_tickets",
                column: "recount_requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_attribute_types_type_name_branch_id",
                table: "product_attribute_types",
                columns: new[] { "type_name", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_goods_issue_attachments_issue",
                table: "goods_issue_attachments",
                column: "goods_issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_issue_items_issue",
                table: "goods_issue_items",
                column: "goods_issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_issues_branch",
                table: "goods_issues",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_issues_number",
                table: "goods_issues",
                column: "issue_number");

            migrationBuilder.CreateIndex(
                name: "ix_store_approvals_status",
                table: "store_approvals",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_store_approvals_user",
                table: "store_approvals",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyClaims_ClaimedByUserId",
                table: "WarrantyClaims",
                column: "ClaimedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyClaims_ProcessedByUserId",
                table: "WarrantyClaims",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyClaims_ProductId",
                table: "WarrantyClaims",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyClaims_SupplierId",
                table: "WarrantyClaims",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyClaims_TicketItemId",
                table: "WarrantyClaims",
                column: "TicketItemId");

            migrationBuilder.AddForeignKey(
                name: "stock_tickets_recount_requested_by_user_id_fkey",
                table: "stock_tickets",
                column: "recount_requested_by_user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "stock_tickets_recount_requested_by_user_id_fkey",
                table: "stock_tickets");

            migrationBuilder.DropTable(
                name: "goods_issue_attachments");

            migrationBuilder.DropTable(
                name: "goods_issue_items");

            migrationBuilder.DropTable(
                name: "store_approvals");

            migrationBuilder.DropTable(
                name: "WarrantyClaims");

            migrationBuilder.DropTable(
                name: "goods_issues");

            migrationBuilder.DropIndex(
                name: "IX_stock_tickets_recount_requested_by_user_id",
                table: "stock_tickets");

            migrationBuilder.DropIndex(
                name: "ix_product_attribute_types_type_name_branch_id",
                table: "product_attribute_types");

            migrationBuilder.DropColumn(
                name: "recount_number",
                table: "stock_tickets");

            migrationBuilder.DropColumn(
                name: "recount_reason",
                table: "stock_tickets");

            migrationBuilder.DropColumn(
                name: "recount_requested_at",
                table: "stock_tickets");

            migrationBuilder.DropColumn(
                name: "recount_requested_by_user_id",
                table: "stock_tickets");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "stock_ticket_items");

            migrationBuilder.RenameColumn(
                name: "unit_name",
                table: "stock_ticket_items",
                newName: "UnitName");

            migrationBuilder.CreateIndex(
                name: "ix_product_attribute_types_type_name",
                table: "product_attribute_types",
                column: "type_name",
                unique: true);
        }
    }
}
