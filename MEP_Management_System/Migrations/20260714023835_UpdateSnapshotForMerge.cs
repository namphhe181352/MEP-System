using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEP_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSnapshotForMerge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Invoice",
                newName: "invoices");

            migrationBuilder.AlterColumn<bool>(
                name: "is_active",
                table: "order_discount_tiers",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                table: "order_discount_tiers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "invoice_id",
                table: "invoices",
                type: "uuid",
                nullable: false,
                defaultValueSql: "uuid_generate_v4()");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "invoices",
                type: "timestamp without time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "held_at",
                table: "invoices",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hold_note",
                table: "invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_code",
                table: "invoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "order_id",
                table: "invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "invoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "invoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_amount",
                table: "invoices",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "invoices_pkey",
                table: "invoices",
                column: "invoice_id");

            migrationBuilder.CreateTable(
                name: "community_categories",
                columns: table => new
                {
                    category_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    category_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("community_categories_pkey", x => x.category_id);
                    table.ForeignKey(
                        name: "community_categories_parent_id_fkey",
                        column: x => x.parent_id,
                        principalTable: "community_categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                name: "otp_verifications",
                columns: table => new
                {
                    otp_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    otp_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    expired_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    is_used = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)0),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("otp_verifications_pkey", x => x.otp_id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    cash_received = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    change_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    viet_qr_string = table.Column<string>(type: "text", nullable: true),
                    qr_expired_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("payments_pkey", x => x.payment_id);
                    table.ForeignKey(
                        name: "payments_invoice_id_fkey",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "invoice_id");
                });

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

            migrationBuilder.CreateTable(
                name: "promotions",
                columns: table => new
                {
                    promotion_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    promotion_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    discount_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    discount_value = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    min_order_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    max_discount_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    start_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    usage_limit = table.Column<int>(type: "integer", nullable: true),
                    used_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("promotions_pkey", x => x.promotion_id);
                });

            migrationBuilder.CreateTable(
                name: "receipts",
                columns: table => new
                {
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    receipt_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    html_content = table.Column<string>(type: "text", nullable: true),
                    printed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("receipts_pkey", x => x.receipt_id);
                    table.ForeignKey(
                        name: "receipts_invoice_id_fkey",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "invoice_id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    suspended_until = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    subscription_plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultBranchId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pkey", x => x.user_id);
                    table.ForeignKey(
                        name: "users_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

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

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    branch_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    branch_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    manager_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("branches_pkey", x => x.branch_id);
                    table.ForeignKey(
                        name: "FK_branches_users_manager_user_id",
                        column: x => x.manager_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                        principalColumn: "category_id");
                    table.ForeignKey(
                        name: "community_posts_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

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
                    table.ForeignKey(
                        name: "expense_categories_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                        name: "store_approvals_reviewed_by_fkey",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "store_approvals_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    supplier_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    contact_person = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tax_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    group_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bank_account = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    website = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)1),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    average_rating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    rating_count = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    last_rated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("suppliers_pkey", x => x.supplier_id);
                    table.ForeignKey(
                        name: "suppliers_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "suppliers_updated_by_fkey",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("system_logs_pkey", x => x.log_id);
                    table.ForeignKey(
                        name: "system_logs_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "system_notifications",
                columns: table => new
                {
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    target = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_urgent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "user_permissions",
                columns: table => new
                {
                    user_permission_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_permissions_pkey", x => x.user_permission_id);
                    table.ForeignKey(
                        name: "user_permissions_permission_id_fkey",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "permission_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "user_permissions_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    group = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    total_spent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true, defaultValue: 0m),
                    order_count = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    last_visit = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    membership_tier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("customers_pkey", x => x.customer_id);
                    table.ForeignKey(
                        name: "FK_customers_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "branch_id");
                    table.ForeignKey(
                        name: "FK_customers_users_CreatedByUserUserId",
                        column: x => x.CreatedByUserUserId,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "daily_summaries",
                columns: table => new
                {
                    summary_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    summary_date = table.Column<DateTime>(type: "date", nullable: false),
                    total_revenue = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false, defaultValue: 0m),
                    total_orders = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    total_refunds = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false, defaultValue: 0m),
                    total_cash = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false, defaultValue: 0m),
                    total_transfer = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false, defaultValue: 0m),
                    total_debt = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false, defaultValue: 0m),
                    opening_cash = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false, defaultValue: 0m),
                    closing_cash = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false, defaultValue: 0m),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("daily_summaries_pkey", x => x.summary_id);
                    table.ForeignKey(
                        name: "daily_summaries_branch_id_fkey",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shifts",
                columns: table => new
                {
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    shift_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "OPEN"),
                    started_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    opening_balance = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    expected_cash = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    actual_cash = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    variance = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    total_revenue = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false, defaultValue: 0m),
                    total_orders = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("shifts_pkey", x => x.shift_id);
                    table.ForeignKey(
                        name: "shifts_branch_id_fkey",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "shifts_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "expense_vouchers",
                columns: table => new
                {
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    voucher_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                        name: "expense_vouchers_cancelled_by_fkey",
                        column: x => x.cancelled_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "expense_vouchers_category_id_fkey",
                        column: x => x.category_id,
                        principalTable: "expense_categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "expense_vouchers_confirmed_by_fkey",
                        column: x => x.confirmed_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "expense_vouchers_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "expense_vouchers_supplier_id_fkey",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "supplier_id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("products_pkey", x => x.product_id);
                    table.ForeignKey(
                        name: "products_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "products_supplier_id_fkey",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "supplier_id");
                    table.ForeignKey(
                        name: "products_updated_by_fkey",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                        name: "purchase_orders_approved_by_fkey",
                        column: x => x.approved_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "purchase_orders_supplier_id_fkey",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "supplier_id");
                    table.ForeignKey(
                        name: "purchase_orders_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "stock_tickets",
                columns: table => new
                {
                    stock_ticket_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ticket_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    ticket_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("stock_tickets_pkey", x => x.stock_ticket_id);
                    table.ForeignKey(
                        name: "stock_tickets_branch_id_fkey",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "branch_id");
                    table.ForeignKey(
                        name: "stock_tickets_cancelled_by_fkey",
                        column: x => x.cancelled_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "stock_tickets_confirmed_by_fkey",
                        column: x => x.confirmed_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "stock_tickets_supplier_id_fkey",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "supplier_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "stock_tickets_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "loyalty_transactions",
                columns: table => new
                {
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reference_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("loyalty_transactions_pkey", x => x.transaction_id);
                    table.ForeignKey(
                        name: "loyalty_transactions_customer_id_fkey",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    PromotionCode = table.Column<string>(type: "text", nullable: true),
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
                        principalColumn: "branch_id");
                    table.ForeignKey(
                        name: "orders_customer_id_fkey",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "customer_id");
                    table.ForeignKey(
                        name: "orders_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "promotion_usages",
                columns: table => new
                {
                    usage_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    promotion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    discount_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("promotion_usages_pkey", x => x.usage_id);
                    table.ForeignKey(
                        name: "promotion_usages_customer_id_fkey",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "customer_id");
                    table.ForeignKey(
                        name: "promotion_usages_invoice_id_fkey",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "invoice_id");
                    table.ForeignKey(
                        name: "promotion_usages_promotion_id_fkey",
                        column: x => x.promotion_id,
                        principalTable: "promotions",
                        principalColumn: "promotion_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "promotion_usages_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

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
                        name: "violation_reports_resolved_by_fkey",
                        column: x => x.resolved_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "violation_reports_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

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
                    reserved_stock = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    available_stock = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    minimum_stock = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true, defaultValue: 10m),
                    maximum_stock = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true, defaultValue: 100m),
                    shelf_location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)1),
                    direct_sale = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)1),
                    estimated_out_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("branch_products_pkey", x => x.branch_product_id);
                    table.ForeignKey(
                        name: "branch_products_branch_id_fkey",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "branch_id");
                    table.ForeignKey(
                        name: "branch_products_product_id_fkey",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id");
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

            migrationBuilder.CreateTable(
                name: "media_resources",
                columns: table => new
                {
                    media_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    media_url = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    media_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_primary = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)0),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
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
                        principalColumn: "product_id");
                    table.ForeignKey(
                        name: "purchase_order_items_purchase_order_id_fkey",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "purchase_order_id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("supplier_ratings_pkey", x => x.rating_id);
                    table.ForeignKey(
                        name: "supplier_ratings_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
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
                    table.ForeignKey(
                        name: "supplier_ratings_supplier_id_fkey",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "supplier_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPayments",
                columns: table => new
                {
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReferenceCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsDeleted = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPayments", x => x.PaymentId);
                    table.ForeignKey(
                        name: "FK_SupplierPayments_purchase_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "purchase_order_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierPayments_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "supplier_id");
                    table.ForeignKey(
                        name: "FK_SupplierPayments_users_RecordedBy",
                        column: x => x.RecordedBy,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "supplier_payments_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "inventory_check_tickets",
                columns: table => new
                {
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ticket_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    filled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    filled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    cancelled_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    linked_stock_ticket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    recount_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    recount_requested_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    recount_requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recount_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("inventory_check_tickets_pkey", x => x.ticket_id);
                    table.ForeignKey(
                        name: "inventory_check_tickets_approved_by_user_id_fkey",
                        column: x => x.approved_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "inventory_check_tickets_assignee_user_id_fkey",
                        column: x => x.assignee_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "inventory_check_tickets_branch_id_fkey",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "branch_id");
                    table.ForeignKey(
                        name: "inventory_check_tickets_cancelled_by_user_id_fkey",
                        column: x => x.cancelled_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "inventory_check_tickets_created_by_user_id_fkey",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "inventory_check_tickets_filled_by_user_id_fkey",
                        column: x => x.filled_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "inventory_check_tickets_linked_stock_ticket_id_fkey",
                        column: x => x.linked_stock_ticket_id,
                        principalTable: "stock_tickets",
                        principalColumn: "stock_ticket_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "inventory_check_tickets_recount_requested_by_user_id_fkey",
                        column: x => x.recount_requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "refund_records",
                columns: table => new
                {
                    refund_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    refund_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    refund_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    reason = table.Column<string>(type: "text", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("refund_records_pkey", x => x.refund_id);
                    table.ForeignKey(
                        name: "refund_records_customer_id_fkey",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "refund_records_invoice_id_fkey",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "invoice_id");
                    table.ForeignKey(
                        name: "refund_records_order_id_fkey",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "refund_records_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    return_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                        principalColumn: "order_id");
                    table.ForeignKey(
                        name: "return_orders_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "invoice_items",
                columns: table => new
                {
                    invoice_item_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    sell_price = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    SelectedUnit = table.Column<string>(type: "text", nullable: true),
                    ConvertValue = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("invoice_items_pkey", x => x.invoice_item_id);
                    table.ForeignKey(
                        name: "invoice_items_branch_product_id_fkey",
                        column: x => x.branch_product_id,
                        principalTable: "branch_products",
                        principalColumn: "branch_product_id");
                    table.ForeignKey(
                        name: "invoice_items_invoice_id_fkey",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "invoice_id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                        principalColumn: "branch_product_id");
                    table.ForeignKey(
                        name: "order_items_order_id_fkey",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                        principalColumn: "branch_product_id");
                    table.ForeignKey(
                        name: "stock_ticket_items_stock_ticket_id_fkey",
                        column: x => x.stock_ticket_id,
                        principalTable: "stock_tickets",
                        principalColumn: "stock_ticket_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_check_details",
                columns: table => new
                {
                    detail_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    system_quantity = table.Column<int>(type: "integer", nullable: false),
                    actual_quantity = table.Column<int>(type: "integer", nullable: false),
                    discrepancy = table.Column<int>(type: "integer", nullable: false),
                    is_counted = table.Column<bool>(type: "boolean", nullable: false),
                    reason_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    counted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    counted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("inventory_check_details_pkey", x => x.detail_id);
                    table.ForeignKey(
                        name: "FK_inventory_check_details_inventory_check_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "inventory_check_tickets",
                        principalColumn: "ticket_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_check_details_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id");
                    table.ForeignKey(
                        name: "inventory_check_details_counted_by_user_id_fkey",
                        column: x => x.counted_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_check_id = table.Column<Guid>(type: "uuid", nullable: true),
                    triggered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("notifications_pkey", x => x.notification_id);
                    table.ForeignKey(
                        name: "notifications_branch_id_fkey",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "branch_id");
                    table.ForeignKey(
                        name: "notifications_inventory_check_id_fkey",
                        column: x => x.inventory_check_id,
                        principalTable: "inventory_check_tickets",
                        principalColumn: "ticket_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "notifications_recipient_user_id_fkey",
                        column: x => x.recipient_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "notifications_triggered_by_user_id_fkey",
                        column: x => x.triggered_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

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
                        principalColumn: "branch_product_id");
                    table.ForeignKey(
                        name: "return_items_return_order_id_fkey",
                        column: x => x.return_order_id,
                        principalTable: "return_orders",
                        principalColumn: "return_order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_discount_tiers_branch_id",
                table: "order_discount_tiers",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "invoices_invoice_code_key",
                table: "invoices",
                column: "invoice_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_order_id",
                table: "invoices",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_user_id",
                table: "invoices",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_branch_products_product_id",
                table: "branch_products",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "uk_branch_product",
                table: "branch_products",
                columns: new[] { "branch_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_branches_manager_user_id",
                table: "branches",
                column: "manager_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_comments_post_id",
                table: "comments",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "IX_comments_user_id",
                table: "comments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_categories_parent_id",
                table: "community_categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_community_categories_slug",
                table: "community_categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_posts_category_id",
                table: "community_posts",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_posts_user_id",
                table: "community_posts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "customers_phone_unique",
                table: "customers",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_BranchId",
                table: "customers",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_customers_CreatedByUserUserId",
                table: "customers",
                column: "CreatedByUserUserId");

            migrationBuilder.CreateIndex(
                name: "IX_daily_summaries_branch_id",
                table: "daily_summaries",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "expense_categories_category_name_key",
                table: "expense_categories",
                column: "category_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expense_categories_created_by",
                table: "expense_categories",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "expense_vouchers_voucher_code_key",
                table: "expense_vouchers",
                column: "voucher_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_vouchers_branch_id",
                table: "expense_vouchers",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_vouchers_cancelled_by",
                table: "expense_vouchers",
                column: "cancelled_by");

            migrationBuilder.CreateIndex(
                name: "ix_expense_vouchers_category_id",
                table: "expense_vouchers",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_vouchers_confirmed_by",
                table: "expense_vouchers",
                column: "confirmed_by");

            migrationBuilder.CreateIndex(
                name: "IX_expense_vouchers_created_by",
                table: "expense_vouchers",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_expense_vouchers_status",
                table: "expense_vouchers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_expense_vouchers_supplier_id",
                table: "expense_vouchers",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_check_details_counted_by_user_id",
                table: "inventory_check_details",
                column: "counted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_check_details_product_id",
                table: "inventory_check_details",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_check_details_ticket_id",
                table: "inventory_check_details",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_check_tickets_approved_by_user_id",
                table: "inventory_check_tickets",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_check_tickets_assignee_user_id",
                table: "inventory_check_tickets",
                column: "assignee_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_check_tickets_branch_id",
                table: "inventory_check_tickets",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_check_tickets_cancelled_by_user_id",
                table: "inventory_check_tickets",
                column: "cancelled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_check_tickets_created_by_user_id",
                table: "inventory_check_tickets",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_check_tickets_filled_by_user_id",
                table: "inventory_check_tickets",
                column: "filled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_check_tickets_linked_stock_ticket_id",
                table: "inventory_check_tickets",
                column: "linked_stock_ticket_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_check_tickets_recount_requested_by_user_id",
                table: "inventory_check_tickets",
                column: "recount_requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uk_inventory_check_ticket_code",
                table: "inventory_check_tickets",
                column: "ticket_code",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_branch_product_id",
                table: "invoice_items",
                column: "branch_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_invoice_id",
                table: "invoice_items",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_transactions_customer_id",
                table: "loyalty_transactions",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_media_resources_post_id",
                table: "media_resources",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "IX_media_resources_product_id",
                table: "media_resources",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_branch_id",
                table: "notifications",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_inventory_check_id",
                table: "notifications",
                column: "inventory_check_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_recipient_user_id",
                table: "notifications",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_triggered_by_user_id",
                table: "notifications",
                column: "triggered_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_branch_product_id",
                table: "order_items",
                column: "branch_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_branch_id",
                table: "orders",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_customer_id",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_user_id",
                table: "orders",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "orders_order_code_key",
                table: "orders",
                column: "order_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_invoice_id",
                table: "payments",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "permissions_permission_code_key",
                table: "permissions",
                column: "permission_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_attributes_product_id",
                table: "product_attributes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_conversion_units_product_id",
                table: "product_conversion_units",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_created_by",
                table: "products",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_products_supplier_id",
                table: "products",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_updated_by",
                table: "products",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "products_product_code_key",
                table: "products",
                column: "product_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_promotion_usages_customer_id",
                table: "promotion_usages",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_promotion_usages_invoice_id",
                table: "promotion_usages",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_promotion_usages_promotion_id",
                table: "promotion_usages",
                column: "promotion_id");

            migrationBuilder.CreateIndex(
                name: "IX_promotion_usages_user_id",
                table: "promotion_usages",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_promotions_code",
                table: "promotions",
                column: "promotion_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_items_product_id",
                table: "purchase_order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_items_purchase_order_id",
                table: "purchase_order_items",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_approved_by",
                table: "purchase_orders",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_supplier_id",
                table: "purchase_orders",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_user_id",
                table: "purchase_orders",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "purchase_orders_order_code_key",
                table: "purchase_orders",
                column: "order_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_receipts_code",
                table: "receipts",
                column: "receipt_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipts_invoice_id",
                table: "receipts",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_records_code",
                table: "refund_records",
                column: "refund_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refund_records_customer_id",
                table: "refund_records",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_refund_records_invoice_id",
                table: "refund_records",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_refund_records_order_id",
                table: "refund_records",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_refund_records_user_id",
                table: "refund_records",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_items_branch_product_id",
                table: "return_items",
                column: "branch_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_items_return_order_id",
                table: "return_items",
                column: "return_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_orders_order_id",
                table: "return_orders",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_orders_user_id",
                table: "return_orders",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "return_orders_return_code_key",
                table: "return_orders",
                column: "return_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_permission_id",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "roles_role_name_key",
                table: "roles",
                column: "role_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shifts_branch_id",
                table: "shifts",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_shifts_code",
                table: "shifts",
                column: "shift_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shifts_user_id",
                table: "shifts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_assignments_branch_id",
                table: "staff_assignments",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_assignments_user_id",
                table: "staff_assignments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ticket_items_branch_product_id",
                table: "stock_ticket_items",
                column: "branch_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ticket_items_stock_ticket_id",
                table: "stock_ticket_items",
                column: "stock_ticket_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_tickets_branch_id",
                table: "stock_tickets",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_tickets_cancelled_by",
                table: "stock_tickets",
                column: "cancelled_by");

            migrationBuilder.CreateIndex(
                name: "IX_stock_tickets_confirmed_by",
                table: "stock_tickets",
                column: "confirmed_by");

            migrationBuilder.CreateIndex(
                name: "IX_stock_tickets_supplier_id",
                table: "stock_tickets",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_tickets_user_id",
                table: "stock_tickets",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "stock_tickets_ticket_code_key",
                table: "stock_tickets",
                column: "ticket_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_store_approvals_reviewed_by",
                table: "store_approvals",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_store_approvals_user_id",
                table: "store_approvals",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_ratings_created_by",
                table: "supplier_ratings",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_ratings_purchase_order_id",
                table: "supplier_ratings",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_ratings_rated_at",
                table: "supplier_ratings",
                column: "rated_at");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_ratings_rated_by",
                table: "supplier_ratings",
                column: "rated_by");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_ratings_supplier_id",
                table: "supplier_ratings",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_created_by",
                table: "SupplierPayments",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_PurchaseOrderId",
                table: "SupplierPayments",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_RecordedBy",
                table: "SupplierPayments",
                column: "RecordedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_SupplierId",
                table: "SupplierPayments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_created_by",
                table: "suppliers",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_updated_by",
                table: "suppliers",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_system_configs_branch_id",
                table: "system_configs",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_system_logs_user_id",
                table: "system_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_system_notifications_created_by",
                table: "system_notifications",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_user_permissions_permission_id",
                table: "user_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_permissions_user_id",
                table: "user_permissions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_created_by",
                table: "users",
                column: "created_by");

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

            migrationBuilder.CreateIndex(
                name: "IX_violation_reports_comment_id",
                table: "violation_reports",
                column: "comment_id");

            migrationBuilder.CreateIndex(
                name: "IX_violation_reports_post_id",
                table: "violation_reports",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "IX_violation_reports_resolved_by",
                table: "violation_reports",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "IX_violation_reports_user_id",
                table: "violation_reports",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "invoices_order_id_fkey",
                table: "invoices",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "order_id");

            migrationBuilder.AddForeignKey(
                name: "invoices_user_id_fkey",
                table: "invoices",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_order_discount_tiers_branches_branch_id",
                table: "order_discount_tiers",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "branch_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "invoices_order_id_fkey",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "invoices_user_id_fkey",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_order_discount_tiers_branches_branch_id",
                table: "order_discount_tiers");

            migrationBuilder.DropTable(
                name: "daily_summaries");

            migrationBuilder.DropTable(
                name: "expense_vouchers");

            migrationBuilder.DropTable(
                name: "inventory_check_details");

            migrationBuilder.DropTable(
                name: "inventory_transfer_details");

            migrationBuilder.DropTable(
                name: "invoice_items");

            migrationBuilder.DropTable(
                name: "loyalty_transactions");

            migrationBuilder.DropTable(
                name: "media_resources");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "otp_verifications");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "product_attributes");

            migrationBuilder.DropTable(
                name: "product_conversion_units");

            migrationBuilder.DropTable(
                name: "promotion_usages");

            migrationBuilder.DropTable(
                name: "purchase_order_items");

            migrationBuilder.DropTable(
                name: "receipts");

            migrationBuilder.DropTable(
                name: "refund_records");

            migrationBuilder.DropTable(
                name: "return_items");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "shifts");

            migrationBuilder.DropTable(
                name: "staff_assignments");

            migrationBuilder.DropTable(
                name: "stock_ticket_items");

            migrationBuilder.DropTable(
                name: "store_approvals");

            migrationBuilder.DropTable(
                name: "supplier_ratings");

            migrationBuilder.DropTable(
                name: "SupplierPayments");

            migrationBuilder.DropTable(
                name: "system_configs");

            migrationBuilder.DropTable(
                name: "system_logs");

            migrationBuilder.DropTable(
                name: "system_notifications");

            migrationBuilder.DropTable(
                name: "user_permissions");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "violation_reports");

            migrationBuilder.DropTable(
                name: "expense_categories");

            migrationBuilder.DropTable(
                name: "inventory_transfer_tickets");

            migrationBuilder.DropTable(
                name: "inventory_check_tickets");

            migrationBuilder.DropTable(
                name: "promotions");

            migrationBuilder.DropTable(
                name: "return_orders");

            migrationBuilder.DropTable(
                name: "branch_products");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "stock_tickets");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "community_posts");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "community_categories");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "IX_order_discount_tiers_branch_id",
                table: "order_discount_tiers");

            migrationBuilder.DropPrimaryKey(
                name: "invoices_pkey",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "invoices_invoice_code_key",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_order_id",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_user_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "order_discount_tiers");

            migrationBuilder.DropColumn(
                name: "invoice_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "held_at",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "hold_note",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "invoice_code",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "note",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "order_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "status",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "total_amount",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "invoices");

            migrationBuilder.RenameTable(
                name: "invoices",
                newName: "Invoice");

            migrationBuilder.AlterColumn<bool>(
                name: "is_active",
                table: "order_discount_tiers",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);
        }
    }
}
