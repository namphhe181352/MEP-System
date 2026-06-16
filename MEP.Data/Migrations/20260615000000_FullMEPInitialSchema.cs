using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP.Data.Migrations
{
    /// <summary>
    /// Full MEP Initial Schema - Tái thiết kế theo SRS Report 3 mục 1.4.5.
    /// Bao gồm 25 entities chính thức + 4 entities phụ trợ (OtpVerification, MediaResource, ProductAttribute, ProductConversionUnit).
    /// </summary>
    public partial class FullMEPInitialSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            // ============================================================
            // 1. BẢNG BRANCHES (SRS: branch_management)
            // ============================================================
            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    branch_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("branches_pkey", x => x.branch_id);
                });

            // ============================================================
            // 2. BẢNG ROLES
            // ============================================================
            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    role_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_system = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("roles_pkey", x => x.role_id);
                });

            migrationBuilder.CreateIndex(
                name: "roles_role_name_key",
                table: "roles",
                column: "role_name",
                unique: true);

            // ============================================================
            // 3. BẢNG PERMISSIONS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    permission_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    permission_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("permissions_pkey", x => x.permission_id);
                });

            migrationBuilder.CreateIndex(
                name: "permissions_permission_code_key",
                table: "permissions",
                column: "permission_code",
                unique: true);

            // ============================================================
            // 4. BẢNG USERS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)1),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pkey", x => x.user_id);
                });

            migrationBuilder.CreateIndex(
                name: "users_email_unique",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "users_phone_unique",
                table: "users",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "users_username_key",
                table: "users",
                column: "username",
                unique: true);

            // ============================================================
            // 5. BẢNG USER_ROLES (n-n)
            // ============================================================
            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_roles_pkey", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "user_roles_role_id_fkey",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "user_roles_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================================================
            // 6. BẢNG ROLE_PERMISSIONS (n-n)
            // ============================================================
            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("role_permissions_pkey", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "role_permissions_permission_id_fkey",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "permission_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "role_permissions_role_id_fkey",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================================================
            // 7. BẢNG SUPPLIERS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    supplier_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)1),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("suppliers_pkey", x => x.supplier_id);
                });

            // ============================================================
            // 8. BẢNG PRODUCTS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    product_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    product_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    base_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    specification = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    item_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    brand_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    category_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    weight = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    weight_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    width = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    length = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    height = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    size_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    sales_channel_linked = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)1),
                    status = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)1),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("products_pkey", x => x.product_id);
                    table.ForeignKey(
                        name: "products_supplier_id_fkey",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "supplier_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "products_product_code_key",
                table: "products",
                column: "product_code",
                unique: true);

            // ============================================================
            // 9. BẢNG BRANCH_PRODUCTS (tồn kho per branch)
            // ============================================================
            migrationBuilder.CreateTable(
                name: "branch_products",
                columns: table => new
                {
                    branch_product_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cost_price = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    sell_price = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    actual_stock = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    available_stock = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    reserved_stock = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    minimum_stock = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 10m),
                    maximum_stock = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 100m),
                    is_active = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)1),
                    direct_sale = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)1),
                    shelf_location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    estimated_out_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("branch_products_pkey", x => x.branch_product_id);
                    table.ForeignKey(
                        name: "branch_products_branch_id_fkey",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "branch_products_product_id_fkey",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uk_branch_product",
                table: "branch_products",
                columns: new[] { "branch_id", "product_id" },
                unique: true);

            // ============================================================
            // 10. BẢNG COMMUNITY_CATEGORIES
            // ============================================================
            migrationBuilder.CreateTable(
                name: "community_categories",
                columns: table => new
                {
                    category_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    category_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("community_categories_pkey", x => x.category_id);
                });

            // ============================================================
            // 12. BẢNG COMMUNITY_POSTS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "community_posts",
                columns: table => new
                {
                    post_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValue: "ACTIVE"),
                    is_pinned = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)0),
                    view_count = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    group_buying_target = table.Column<int>(type: "integer", nullable: true),
                    group_buying_price = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("community_posts_pkey", x => x.post_id);
                    table.ForeignKey(
                        name: "community_posts_category_id_fkey",
                        column: x => x.category_id,
                        principalTable: "community_categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "community_posts_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ============================================================
            // 13. BẢNG COMMENTS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    comment_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment_text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("comments_pkey", x => x.comment_id);
                    table.ForeignKey(
                        name: "comments_post_id_fkey",
                        column: x => x.post_id,
                        principalTable: "community_posts",
                        principalColumn: "post_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "comments_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ============================================================
            // 13b. BẢNG MEDIA_RESOURCES (chèn ở đây vì cần products + community_posts)
            // ============================================================
            migrationBuilder.CreateTable(
                name: "media_resources",
                columns: table => new
                {
                    media_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    media_url = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    media_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, defaultValue: "IMAGE"),
                    is_primary = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)0),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    post_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("media_resources_pkey", x => x.media_id);
                    table.ForeignKey(
                        name: "media_resources_post_id_fkey",
                        column: x => x.post_id,
                        principalTable: "community_posts",
                        principalColumn: "post_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "media_resources_product_id_fkey",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================================================
            // 14. BẢNG VIOLATION_REPORTS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "violation_reports",
                columns: table => new
                {
                    report_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValueSql: "'PENDING'::character varying"),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    admin_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("violation_reports_pkey", x => x.report_id);
                    table.ForeignKey(
                        name: "violation_reports_comment_id_fkey",
                        column: x => x.comment_id,
                        principalTable: "comments",
                        principalColumn: "comment_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "violation_reports_post_id_fkey",
                        column: x => x.post_id,
                        principalTable: "community_posts",
                        principalColumn: "post_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "violation_reports_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "violation_reports_resolved_by_fkey",
                        column: x => x.resolved_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ============================================================
            // 15. BẢNG CUSTOMERS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    customer_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    loyalty_points = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    debt_limit = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true, defaultValue: 0m),
                    current_debt = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true, defaultValue: 0m),
                    status = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)1),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("customers_pkey", x => x.customer_id);
                });

            migrationBuilder.CreateIndex(
                name: "customers_phone_unique",
                table: "customers",
                column: "phone_number",
                unique: true);

            // ============================================================
            // 16. BẢNG ORDERS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    order_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    order_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false, defaultValue: 0m),
                    discount_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    tax_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("orders_pkey", x => x.order_id);
                    table.ForeignKey(
                        name: "orders_branch_id_fkey",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "orders_customer_id_fkey",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "orders_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "orders_order_code_key",
                table: "orders",
                column: "order_code",
                unique: true);

            // ============================================================
            // 17. BẢNG ORDER_ITEMS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    sell_price = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("order_items_pkey", x => x.order_item_id);
                    table.ForeignKey(
                        name: "order_items_branch_product_id_fkey",
                        column: x => x.branch_product_id,
                        principalTable: "branch_products",
                        principalColumn: "branch_product_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "order_items_order_id_fkey",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================================================
            // 18. BẢNG INVOICES
            // ============================================================
            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    invoice_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("invoices_pkey", x => x.invoice_id);
                    table.ForeignKey(
                        name: "invoices_order_id_fkey",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "invoices_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "invoices_invoice_code_key",
                table: "invoices",
                column: "invoice_code",
                unique: true);

            // ============================================================
            // 19. BẢNG INVOICE_ITEMS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "invoice_items",
                columns: table => new
                {
                    invoice_item_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    sell_price = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("invoice_items_pkey", x => x.invoice_item_id);
                    table.ForeignKey(
                        name: "invoice_items_branch_product_id_fkey",
                        column: x => x.branch_product_id,
                        principalTable: "branch_products",
                        principalColumn: "branch_product_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "invoice_items_invoice_id_fkey",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "invoice_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================================================
            // 20. BẢNG PURCHASE_ORDERS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    order_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true, defaultValueSql: "0"),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    expected_delivery_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("purchase_orders_pkey", x => x.purchase_order_id);
                    table.ForeignKey(
                        name: "purchase_orders_supplier_id_fkey",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "supplier_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "purchase_orders_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "purchase_orders_approved_by_fkey",
                        column: x => x.approved_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "purchase_orders_order_code_key",
                table: "purchase_orders",
                column: "order_code",
                unique: true);

            // ============================================================
            // 21. BẢNG PURCHASE_ORDER_ITEMS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "purchase_order_items",
                columns: table => new
                {
                    po_item_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("purchase_order_items_pkey", x => x.po_item_id);
                    table.ForeignKey(
                        name: "purchase_order_items_product_id_fkey",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "purchase_order_items_purchase_order_id_fkey",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "purchase_order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================================================
            // 22. BẢNG STOCK_TICKETS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "stock_tickets",
                columns: table => new
                {
                    stock_ticket_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ticket_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("stock_tickets_pkey", x => x.stock_ticket_id);
                    table.ForeignKey(
                        name: "stock_tickets_branch_id_fkey",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "stock_tickets_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "stock_tickets_ticket_code_key",
                table: "stock_tickets",
                column: "ticket_code",
                unique: true);

            // ============================================================
            // 23. BẢNG STOCK_TICKET_ITEMS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "stock_ticket_items",
                columns: table => new
                {
                    ticket_item_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    stock_ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    system_quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    actual_quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    discrepancy = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("stock_ticket_items_pkey", x => x.ticket_item_id);
                    table.ForeignKey(
                        name: "stock_ticket_items_branch_product_id_fkey",
                        column: x => x.branch_product_id,
                        principalTable: "branch_products",
                        principalColumn: "branch_product_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "stock_ticket_items_stock_ticket_id_fkey",
                        column: x => x.stock_ticket_id,
                        principalTable: "stock_tickets",
                        principalColumn: "stock_ticket_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================================================
            // 24. BẢNG RETURN_ORDERS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "return_orders",
                columns: table => new
                {
                    return_order_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    return_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    refund_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("return_orders_pkey", x => x.return_order_id);
                    table.ForeignKey(
                        name: "return_orders_order_id_fkey",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "return_orders_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "return_orders_return_code_key",
                table: "return_orders",
                column: "return_code",
                unique: true);

            // ============================================================
            // 25. BẢNG RETURN_ITEMS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "return_items",
                columns: table => new
                {
                    return_item_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    return_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("return_items_pkey", x => x.return_item_id);
                    table.ForeignKey(
                        name: "return_items_branch_product_id_fkey",
                        column: x => x.branch_product_id,
                        principalTable: "branch_products",
                        principalColumn: "branch_product_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "return_items_return_order_id_fkey",
                        column: x => x.return_order_id,
                        principalTable: "return_orders",
                        principalColumn: "return_order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================================================
            // 26. BẢNG STAFF_ASSIGNMENTS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "staff_assignments",
                columns: table => new
                {
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("staff_assignments_pkey", x => x.assignment_id);
                    table.ForeignKey(
                        name: "staff_assignments_branch_id_fkey",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "staff_assignments_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================================================
            // 27. BẢNG SYSTEM_CONFIGS (SRS: app_settings)
            // ============================================================
            migrationBuilder.CreateTable(
                name: "system_configs",
                columns: table => new
                {
                    config_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    config_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    config_value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("system_configs_pkey", x => x.config_id);
                    table.ForeignKey(
                        name: "system_configs_branch_id_fkey",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================================================
            // 28. BẢNG SYSTEM_LOGS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "system_logs",
                columns: table => new
                {
                    log_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    table_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("system_logs_pkey", x => x.log_id);
                    table.ForeignKey(
                        name: "system_logs_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ============================================================
            // 29. BẢNG OTP_VERIFICATIONS
            // ============================================================
            migrationBuilder.CreateTable(
                name: "otp_verifications",
                columns: table => new
                {
                    otp_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    otp_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_used = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)0),
                    expired_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("otp_verifications_pkey", x => x.otp_id);
                });

            // ============================================================
            // 30. BẢNG PRODUCT_ATTRIBUTES
            // ============================================================
            migrationBuilder.CreateTable(
                name: "product_attributes",
                columns: table => new
                {
                    attribute_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribute_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    attribute_value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("product_attributes_pkey", x => x.attribute_id);
                    table.ForeignKey(
                        name: "product_attributes_product_id_fkey",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================================================
            // 31. BẢNG PRODUCT_CONVERSION_UNITS (Multi-UOM, theo UC Process Multi-UOM Conversion)
            // ============================================================
            migrationBuilder.CreateTable(
                name: "product_conversion_units",
                columns: table => new
                {
                    conversion_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    convert_value = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    price = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    direct_sale = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("product_conversion_units_pkey", x => x.conversion_id);
                    table.ForeignKey(
                        name: "product_conversion_units_product_id_fkey",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================================================
            // SEED DATA TỐI THIỂU (chỉ để migration pass + Auth API chạy được)
            // Toàn bộ seed data đầy đủ (User, Supplier, Product, Customer, Order, ...) sẽ
            // được insert bởi DatabaseSeeder.cs lúc startup vì cần BCrypt hash password thật.
            // ============================================================

            // Branches (2 chi nhánh mẫu)
            migrationBuilder.Sql(@"
                INSERT INTO branches (branch_id, branch_name, address) VALUES
                ('30000000-0000-0000-0000-000000000001', 'Chi nhánh Miền Bắc', '123 Cầu Giấy, Hà Nội'),
                ('30000000-0000-0000-0000-000000000002', 'Chi nhánh Miền Nam', '456 Quận 1, TP. Hồ Chí Minh');
            ");

            // Permissions (12 quyền)
            migrationBuilder.Sql(@"
                INSERT INTO permissions (permission_id, permission_code, permission_name) VALUES
                ('00000000-0000-0000-0000-000000000001', 'STOCK_VIEW', 'Xem tồn kho chi nhánh'),
                ('00000000-0000-0000-0000-000000000002', 'STOCK_MANAGE', 'Nhập kho và Điều chỉnh kho'),
                ('00000000-0000-0000-0000-000000000003', 'POS_SALE', 'Thực hiện bán hàng tại quầy'),
                ('00000000-0000-0000-0000-000000000004', 'PO_APPROVE', 'Phê duyệt đơn mua hàng'),
                ('00000000-0000-0000-0000-000000000005', 'COMMUNITY_MOD', 'Kiểm duyệt bài viết diễn đàn'),
                ('00000000-0000-0000-0000-000000000006', 'USER_MANAGE', 'Quản lý tài khoản nhân viên'),
                ('00000000-0000-0000-0000-000000000007', 'BRANCH_MANAGE', 'Quản lý chi nhánh'),
                ('00000000-0000-0000-0000-000000000008', 'REPORT_VIEW', 'Xem báo cáo doanh thu'),
                ('00000000-0000-0000-0000-000000000009', 'RETURN_PROCESS', 'Xử lý đổi trả hàng'),
                ('00000000-0000-0000-0000-000000000010', 'STOCK_TRANSFER', 'Tạo phiếu chuyển kho'),
                ('00000000-0000-0000-0000-000000000011', 'PRODUCT_MANAGE', 'Quản lý sản phẩm toàn hệ thống'),
                ('00000000-0000-0000-0000-000000000012', 'CUSTOMER_MANAGE', 'Quản lý khách hàng');
            ");

            // Roles (5 vai trò)
            migrationBuilder.Sql(@"
                INSERT INTO roles (role_id, role_name, description, is_system) VALUES
                ('10000000-0000-0000-0000-000000000001', 'Admin', 'Admin hệ thống M.E.P (super user)', 1),
                ('10000000-0000-0000-0000-000000000002', 'Owner', 'Chủ chuỗi cửa hàng, toàn quyền chi nhánh', 1),
                ('10000000-0000-0000-0000-000000000003', 'SalesStaff', 'Nhân viên bán hàng tại quầy POS', 0),
                ('10000000-0000-0000-0000-000000000004', 'InventoryStaff', 'Nhân viên quản lý kho bãi', 0),
                ('10000000-0000-0000-0000-000000000005', 'CommunityUser', 'Thành viên diễn đàn cộng đồng', 0);
            ");

            // SystemConfigs (VAT_RATE cho mỗi branch)
            migrationBuilder.Sql(@"
                INSERT INTO system_configs (config_id, branch_id, config_key, config_value) VALUES
                ('50000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001', 'VAT_RATE', '10'),
                ('50000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000001', 'ALLOW_PRICE_EDITING', 'FALSE'),
                ('50000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000002', 'VAT_RATE', '10'),
                ('50000000-0000-0000-0000-000000000004', '30000000-0000-0000-0000-000000000002', 'ALLOW_PRICE_EDITING', 'TRUE');
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop tất cả bảng theo thứ tự ngược lại
            migrationBuilder.DropTable(name: "system_logs");
            migrationBuilder.DropTable(name: "system_configs");
            migrationBuilder.DropTable(name: "staff_assignments");
            migrationBuilder.DropTable(name: "return_items");
            migrationBuilder.DropTable(name: "return_orders");
            migrationBuilder.DropTable(name: "stock_ticket_items");
            migrationBuilder.DropTable(name: "stock_tickets");
            migrationBuilder.DropTable(name: "purchase_order_items");
            migrationBuilder.DropTable(name: "purchase_orders");
            migrationBuilder.DropTable(name: "invoice_items");
            migrationBuilder.DropTable(name: "invoices");
            migrationBuilder.DropTable(name: "order_items");
            migrationBuilder.DropTable(name: "orders");
            migrationBuilder.DropTable(name: "customers");
            migrationBuilder.DropTable(name: "violation_reports");
            migrationBuilder.DropTable(name: "comments");
            migrationBuilder.DropTable(name: "media_resources");
            migrationBuilder.DropTable(name: "community_posts");
            migrationBuilder.DropTable(name: "community_categories");
            migrationBuilder.DropTable(name: "product_conversion_units");
            migrationBuilder.DropTable(name: "product_attributes");
            migrationBuilder.DropTable(name: "otp_verifications");
            migrationBuilder.DropTable(name: "branch_products");
            migrationBuilder.DropTable(name: "products");
            migrationBuilder.DropTable(name: "suppliers");
            migrationBuilder.DropTable(name: "user_roles");
            migrationBuilder.DropTable(name: "role_permissions");
            migrationBuilder.DropTable(name: "users");
            migrationBuilder.DropTable(name: "permissions");
            migrationBuilder.DropTable(name: "roles");
            migrationBuilder.DropTable(name: "branches");
        }
    }
}
