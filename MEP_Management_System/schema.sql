CREATE EXTENSION IF NOT EXISTS "uuid-ossp";


CREATE TABLE inventory_transfer_tickets (
    transfer_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    transfer_code character varying(50) NOT NULL,
    from_branch_id uuid NOT NULL,
    to_branch_id uuid NOT NULL,
    status character varying(50) NOT NULL,
    created_by_user_id uuid NOT NULL,
    created_at timestamp without time zone NOT NULL,
    notes character varying(1000),
    CONSTRAINT inventory_transfer_tickets_pkey PRIMARY KEY (transfer_id)
);


CREATE TABLE otp_verifications (
    otp_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    email character varying(150) NOT NULL,
    otp_code character varying(6) NOT NULL,
    purpose character varying(50) NOT NULL,
    expired_at timestamp without time zone NOT NULL,
    is_used smallint DEFAULT 0,
    created_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT otp_verifications_pkey PRIMARY KEY (otp_id)
);


CREATE TABLE permissions (
    permission_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    permission_code character varying(100) NOT NULL,
    permission_name character varying(100) NOT NULL,
    CONSTRAINT permissions_pkey PRIMARY KEY (permission_id)
);


CREATE TABLE product_attribute_types (
    type_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    type_name character varying(100) NOT NULL,
    type_code character varying(50),
    is_active smallint NOT NULL DEFAULT 1,
    created_at timestamp without time zone NOT NULL,
    created_by uuid,
    updated_at timestamp without time zone,
    updated_by uuid,
    CONSTRAINT product_attribute_types_pkey PRIMARY KEY (type_id)
);


CREATE TABLE product_locations (
    location_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    location_name character varying(100) NOT NULL,
    location_code character varying(50),
    description character varying(500),
    is_active smallint NOT NULL DEFAULT 1,
    created_at timestamp without time zone NOT NULL,
    created_by uuid,
    updated_at timestamp without time zone,
    updated_by uuid,
    CONSTRAINT product_locations_pkey PRIMARY KEY (location_id)
);


CREATE TABLE roles (
    role_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    role_name character varying(50) NOT NULL,
    description character varying(255),
    is_system smallint DEFAULT 0,
    CONSTRAINT roles_pkey PRIMARY KEY (role_id)
);


CREATE TABLE users (
    user_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    username character varying(50) NOT NULL,
    password_hash character varying(255) NOT NULL,
    full_name character varying(100) NOT NULL,
    is_active smallint DEFAULT 1,
    created_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP),
    email character varying(150) NOT NULL,
    phone_number character varying(20),
    is_verified boolean NOT NULL DEFAULT FALSE,
    status character varying(20),
    "DeletedAt" timestamp without time zone,
    suspended_until timestamp without time zone,
    subscription_plan character varying(20),
    created_by uuid,
    "DefaultBranchId" uuid,
    CONSTRAINT users_pkey PRIMARY KEY (user_id),
    CONSTRAINT users_created_by_fkey FOREIGN KEY (created_by) REFERENCES users (user_id) ON DELETE SET NULL
);


CREATE TABLE role_permissions (
    role_id uuid NOT NULL,
    permission_id uuid NOT NULL,
    CONSTRAINT role_permissions_pkey PRIMARY KEY (role_id, permission_id),
    CONSTRAINT role_permissions_permission_id_fkey FOREIGN KEY (permission_id) REFERENCES permissions (permission_id) ON DELETE CASCADE,
    CONSTRAINT role_permissions_role_id_fkey FOREIGN KEY (role_id) REFERENCES roles (role_id) ON DELETE CASCADE
);


CREATE TABLE branches (
    branch_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    branch_name character varying(100) NOT NULL,
    branch_code character varying(50),
    phone character varying(50),
    city character varying(100),
    address character varying(255),
    type character varying(50),
    manager_user_id uuid,
    is_active smallint NOT NULL DEFAULT 1,
    created_at timestamp without time zone,
    updated_at timestamp without time zone,
    "ReturnDaysAllowed" integer,
    "ExchangeDaysAllowed" integer,
    CONSTRAINT branches_pkey PRIMARY KEY (branch_id),
    CONSTRAINT "FK_branches_users_manager_user_id" FOREIGN KEY (manager_user_id) REFERENCES users (user_id) ON DELETE SET NULL
);


CREATE TABLE password_reset_tokens (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    user_id uuid NOT NULL,
    token_hash character varying(255) NOT NULL,
    expires_at timestamp without time zone NOT NULL,
    used_at timestamp without time zone,
    ip_address character varying(50),
    user_agent character varying(255),
    created_at timestamp without time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT password_reset_tokens_pkey PRIMARY KEY (id),
    CONSTRAINT "FK_password_reset_tokens_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE
);


CREATE TABLE system_logs (
    log_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    user_id uuid NOT NULL,
    action character varying(100) NOT NULL,
    table_name character varying(50) NOT NULL,
    entity_type character varying(50),
    entity_id uuid,
    description text,
    ip_address character varying(50),
    user_agent character varying(255),
    level character varying(20),
    source character varying(100),
    timestamp timestamp without time zone DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT system_logs_pkey PRIMARY KEY (log_id),
    CONSTRAINT system_logs_user_id_fkey FOREIGN KEY (user_id) REFERENCES users (user_id)
);


CREATE TABLE system_notifications (
    notification_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    title character varying(255) NOT NULL,
    content text NOT NULL,
    target character varying(50) NOT NULL,
    branch_id uuid,
    is_urgent boolean NOT NULL DEFAULT FALSE,
    scheduled_at timestamp without time zone,
    sent_at timestamp without time zone,
    status character varying(50) NOT NULL DEFAULT 'DRAFT',
    created_by uuid NOT NULL,
    created_at timestamp without time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT system_notifications_pkey PRIMARY KEY (notification_id),
    CONSTRAINT system_notifications_created_by_fkey FOREIGN KEY (created_by) REFERENCES users (user_id)
);


CREATE TABLE user_permissions (
    user_permission_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    user_id uuid NOT NULL,
    permission_id uuid NOT NULL,
    is_granted boolean,
    CONSTRAINT user_permissions_pkey PRIMARY KEY (user_permission_id),
    CONSTRAINT user_permissions_permission_id_fkey FOREIGN KEY (permission_id) REFERENCES permissions (permission_id) ON DELETE CASCADE,
    CONSTRAINT user_permissions_user_id_fkey FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE
);


CREATE TABLE user_roles (
    user_id uuid NOT NULL,
    role_id uuid NOT NULL,
    CONSTRAINT user_roles_pkey PRIMARY KEY (user_id, role_id),
    CONSTRAINT user_roles_role_id_fkey FOREIGN KEY (role_id) REFERENCES roles (role_id) ON DELETE CASCADE,
    CONSTRAINT user_roles_user_id_fkey FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE
);


CREATE TABLE customers (
    customer_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    customer_name character varying(100) NOT NULL,
    phone_number character varying(20),
    email character varying(150),
    address character varying(255),
    loyalty_points integer DEFAULT 0,
    debt_limit numeric(15,2) DEFAULT 0.0,
    current_debt numeric(15,2) DEFAULT 0.0,
    advance_balance numeric(15,2) DEFAULT 0.0,
    status smallint DEFAULT 1,
    created_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP),
    "group" character varying(50),
    notes text,
    total_spent numeric(18,2) DEFAULT 0.0,
    order_count integer DEFAULT 0,
    last_visit timestamp without time zone,
    membership_tier character varying(50),
    updated_at timestamp without time zone,
    "CreatedBy" uuid,
    "BranchId" uuid,
    "CreatedByUserUserId" uuid,
    CONSTRAINT customers_pkey PRIMARY KEY (customer_id),
    CONSTRAINT "FK_customers_branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES branches (branch_id),
    CONSTRAINT "FK_customers_users_CreatedByUserUserId" FOREIGN KEY ("CreatedByUserUserId") REFERENCES users (user_id)
);


CREATE TABLE expense_categories (
    category_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    category_name character varying(100) NOT NULL,
    is_active smallint NOT NULL DEFAULT 1,
    "BranchId" uuid,
    created_by uuid,
    created_at timestamp without time zone,
    CONSTRAINT expense_categories_pkey PRIMARY KEY (category_id),
    CONSTRAINT "FK_expense_categories_branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES branches (branch_id),
    CONSTRAINT expense_categories_created_by_fkey FOREIGN KEY (created_by) REFERENCES users (user_id) ON DELETE SET NULL
);


CREATE TABLE invoice_print_templates (
    template_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    branch_id uuid,
    header_text character varying(500),
    footer_text character varying(500),
    show_logo boolean NOT NULL DEFAULT FALSE,
    logo_url character varying(500),
    branch_name character varying(200),
    branch_address character varying(500),
    phone character varying(20),
    tax_code character varying(50),
    paper_size character varying(20) NOT NULL DEFAULT 'K80',
    font_size integer NOT NULL DEFAULT 12,
    font_family character varying(50) NOT NULL DEFAULT 'monospace',
    show_customer_info boolean NOT NULL DEFAULT TRUE,
    show_cashier_name boolean NOT NULL DEFAULT TRUE,
    show_branch_info boolean NOT NULL DEFAULT TRUE,
    show_payment_method boolean NOT NULL DEFAULT TRUE,
    thank_you_message character varying(500),
    custom_fields_json text NOT NULL DEFAULT '{}',
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamp without time zone NOT NULL,
    updated_at timestamp without time zone NOT NULL,
    CONSTRAINT invoice_print_templates_pkey PRIMARY KEY (template_id),
    CONSTRAINT "FK_invoice_print_templates_branches_branch_id" FOREIGN KEY (branch_id) REFERENCES branches (branch_id) ON DELETE SET NULL
);


CREATE TABLE order_discount_tiers (
    order_discount_tier_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    branch_id uuid,
    min_order_value numeric(18,2) NOT NULL,
    discount_percent numeric(5,2) NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamp without time zone,
    updated_at timestamp without time zone,
    CONSTRAINT order_discount_tiers_pkey PRIMARY KEY (order_discount_tier_id),
    CONSTRAINT "FK_order_discount_tiers_branches_branch_id" FOREIGN KEY (branch_id) REFERENCES branches (branch_id)
);


CREATE TABLE product_taxonomies (
    taxonomy_id uuid NOT NULL,
    branch_id uuid,
    name character varying(255) NOT NULL,
    type character varying(50) NOT NULL,
    created_at timestamp without time zone NOT NULL,
    CONSTRAINT "PK_product_taxonomies" PRIMARY KEY (taxonomy_id),
    CONSTRAINT "FK_product_taxonomies_branches_branch_id" FOREIGN KEY (branch_id) REFERENCES branches (branch_id)
);


CREATE TABLE shifts (
    shift_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    shift_code character varying(50) NOT NULL,
    user_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    status character varying(20) NOT NULL DEFAULT 'OPEN',
    started_at timestamp without time zone NOT NULL,
    ended_at timestamp without time zone,
    opening_balance numeric(15,2) NOT NULL,
    expected_cash numeric(15,2),
    actual_cash numeric(15,2),
    variance numeric(15,2),
    total_revenue numeric(15,2) NOT NULL DEFAULT 0.0,
    total_orders integer NOT NULL DEFAULT 0,
    note text,
    CONSTRAINT shifts_pkey PRIMARY KEY (shift_id),
    CONSTRAINT shifts_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES branches (branch_id) ON DELETE CASCADE,
    CONSTRAINT shifts_user_id_fkey FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE
);


CREATE TABLE staff_assignments (
    assignment_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    user_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    assigned_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT staff_assignments_pkey PRIMARY KEY (assignment_id),
    CONSTRAINT staff_assignments_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES branches (branch_id) ON DELETE CASCADE,
    CONSTRAINT staff_assignments_user_id_fkey FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE
);


CREATE TABLE stock_print_templates (
    template_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    branch_id uuid,
    ticket_type character varying(50),
    header_text character varying(500),
    footer_text character varying(500),
    show_logo boolean NOT NULL DEFAULT FALSE,
    logo_url character varying(500),
    branch_name character varying(200),
    branch_address character varying(500),
    phone character varying(20),
    tax_code character varying(50),
    paper_size character varying(20) NOT NULL DEFAULT 'A5',
    font_size integer NOT NULL DEFAULT 12,
    font_family character varying(50) NOT NULL DEFAULT 'monospace',
    show_supplier boolean NOT NULL DEFAULT TRUE,
    show_branch_info boolean NOT NULL DEFAULT TRUE,
    show_signature boolean NOT NULL DEFAULT TRUE,
    custom_fields_json text NOT NULL DEFAULT '{}',
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamp without time zone NOT NULL,
    updated_at timestamp without time zone NOT NULL,
    CONSTRAINT stock_print_templates_pkey PRIMARY KEY (template_id),
    CONSTRAINT "FK_stock_print_templates_branches_branch_id" FOREIGN KEY (branch_id) REFERENCES branches (branch_id) ON DELETE SET NULL
);


CREATE TABLE suppliers (
    supplier_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    supplier_name character varying(150) NOT NULL,
    contact_phone character varying(20),
    contact_email character varying(150),
    contact_person character varying(100),
    tax_code character varying(50),
    group_name character varying(50),
    bank_account character varying(50),
    bank_name character varying(100),
    website character varying(255),
    notes text,
    address character varying(255),
    status smallint DEFAULT 1,
    "BranchId" uuid,
    created_by uuid,
    updated_by uuid,
    created_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp without time zone,
    average_rating numeric(3,2),
    rating_count integer DEFAULT 0,
    last_rated_at timestamp without time zone,
    CONSTRAINT suppliers_pkey PRIMARY KEY (supplier_id),
    CONSTRAINT "FK_suppliers_branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES branches (branch_id),
    CONSTRAINT suppliers_created_by_fkey FOREIGN KEY (created_by) REFERENCES users (user_id) ON DELETE SET NULL,
    CONSTRAINT suppliers_updated_by_fkey FOREIGN KEY (updated_by) REFERENCES users (user_id) ON DELETE SET NULL
);


CREATE TABLE system_configs (
    config_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    branch_id uuid,
    config_key character varying(100) NOT NULL,
    config_value text NOT NULL,
    CONSTRAINT system_configs_pkey PRIMARY KEY (config_id),
    CONSTRAINT system_configs_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES branches (branch_id) ON DELETE CASCADE
);


CREATE TABLE orders (
    order_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    order_code character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    customer_id uuid,
    user_id uuid NOT NULL,
    total_amount numeric(15,2) NOT NULL DEFAULT 0.0,
    discount_amount numeric(15,2),
    "PromotionCode" text,
    tax_amount numeric(15,2),
    payment_method character varying(50),
    status character varying(50) NOT NULL,
    note text,
    created_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT orders_pkey PRIMARY KEY (order_id),
    CONSTRAINT orders_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES branches (branch_id),
    CONSTRAINT orders_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES customers (customer_id),
    CONSTRAINT orders_user_id_fkey FOREIGN KEY (user_id) REFERENCES users (user_id)
);


CREATE TABLE expense_vouchers (
    voucher_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    voucher_code character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    category_id uuid,
    supplier_id uuid,
    amount numeric(18,2) NOT NULL,
    reason character varying(500) NOT NULL,
    note text,
    status character varying(20) NOT NULL DEFAULT 'PENDING',
    created_by uuid NOT NULL,
    created_at timestamp without time zone,
    confirmed_by uuid,
    completed_at timestamp without time zone,
    cancelled_by uuid,
    cancelled_at timestamp without time zone,
    cancel_reason character varying(500),
    CONSTRAINT expense_vouchers_pkey PRIMARY KEY (voucher_id),
    CONSTRAINT expense_vouchers_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES branches (branch_id),
    CONSTRAINT expense_vouchers_cancelled_by_fkey FOREIGN KEY (cancelled_by) REFERENCES users (user_id) ON DELETE SET NULL,
    CONSTRAINT expense_vouchers_category_id_fkey FOREIGN KEY (category_id) REFERENCES expense_categories (category_id) ON DELETE SET NULL,
    CONSTRAINT expense_vouchers_confirmed_by_fkey FOREIGN KEY (confirmed_by) REFERENCES users (user_id) ON DELETE SET NULL,
    CONSTRAINT expense_vouchers_created_by_fkey FOREIGN KEY (created_by) REFERENCES users (user_id),
    CONSTRAINT expense_vouchers_supplier_id_fkey FOREIGN KEY (supplier_id) REFERENCES suppliers (supplier_id) ON DELETE SET NULL
);


CREATE TABLE products (
    product_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    product_code character varying(50) NOT NULL,
    product_name character varying(255) NOT NULL,
    barcode character varying(50),
    base_unit character varying(20) NOT NULL,
    specification character varying(100),
    item_type character varying(50),
    "BrandTaxonomyId" uuid,
    "CategoryTaxonomyId" uuid,
    brand_name character varying(100),
    category_name character varying(100),
    supplier_id uuid,
    weight numeric(10,2),
    weight_unit character varying(10),
    width numeric(10,2),
    length numeric(10,2),
    height numeric(10,2),
    size_unit character varying(10),
    sales_channel_linked smallint DEFAULT 1,
    status smallint DEFAULT 1,
    "BranchId" uuid,
    created_by uuid,
    updated_by uuid,
    created_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp without time zone,
    CONSTRAINT products_pkey PRIMARY KEY (product_id),
    CONSTRAINT "FK_products_product_taxonomies_BrandTaxonomyId" FOREIGN KEY ("BrandTaxonomyId") REFERENCES product_taxonomies (taxonomy_id),
    CONSTRAINT "FK_products_product_taxonomies_CategoryTaxonomyId" FOREIGN KEY ("CategoryTaxonomyId") REFERENCES product_taxonomies (taxonomy_id),
    CONSTRAINT products_created_by_fkey FOREIGN KEY (created_by) REFERENCES users (user_id) ON DELETE SET NULL,
    CONSTRAINT products_supplier_id_fkey FOREIGN KEY (supplier_id) REFERENCES suppliers (supplier_id),
    CONSTRAINT products_updated_by_fkey FOREIGN KEY (updated_by) REFERENCES users (user_id) ON DELETE SET NULL
);


CREATE TABLE purchase_orders (
    purchase_order_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    order_code character varying(50),
    supplier_id uuid NOT NULL,
    user_id uuid NOT NULL,
    total_amount numeric(15,2) DEFAULT (0),
    status character varying(50) NOT NULL,
    approved_by uuid,
    approved_at timestamp without time zone,
    expected_delivery_date timestamp without time zone,
    notes text,
    created_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT purchase_orders_pkey PRIMARY KEY (purchase_order_id),
    CONSTRAINT purchase_orders_approved_by_fkey FOREIGN KEY (approved_by) REFERENCES users (user_id),
    CONSTRAINT purchase_orders_supplier_id_fkey FOREIGN KEY (supplier_id) REFERENCES suppliers (supplier_id),
    CONSTRAINT purchase_orders_user_id_fkey FOREIGN KEY (user_id) REFERENCES users (user_id)
);


CREATE TABLE stock_tickets (
    stock_ticket_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    ticket_code character varying(50),
    branch_id uuid NOT NULL,
    user_id uuid NOT NULL,
    supplier_id uuid,
    total_amount numeric(15,2) NOT NULL,
    ticket_type character varying(50),
    status character varying(50),
    notes text,
    completed_at timestamp without time zone,
    confirmed_by uuid,
    cancelled_at timestamp without time zone,
    cancelled_by uuid,
    cancel_reason character varying(500),
    created_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT stock_tickets_pkey PRIMARY KEY (stock_ticket_id),
    CONSTRAINT stock_tickets_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES branches (branch_id),
    CONSTRAINT stock_tickets_cancelled_by_fkey FOREIGN KEY (cancelled_by) REFERENCES users (user_id) ON DELETE SET NULL,
    CONSTRAINT stock_tickets_confirmed_by_fkey FOREIGN KEY (confirmed_by) REFERENCES users (user_id) ON DELETE SET NULL,
    CONSTRAINT stock_tickets_supplier_id_fkey FOREIGN KEY (supplier_id) REFERENCES suppliers (supplier_id) ON DELETE SET NULL,
    CONSTRAINT stock_tickets_user_id_fkey FOREIGN KEY (user_id) REFERENCES users (user_id)
);


CREATE TABLE invoices (
    invoice_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    invoice_code character varying(50),
    order_id uuid NOT NULL,
    user_id uuid NOT NULL,
    sub_total_amount numeric(18,2) NOT NULL DEFAULT 0.0,
    discount_percent numeric(5,2),
    discount_amount numeric(18,2) NOT NULL DEFAULT 0.0,
    total_amount numeric(15,2) NOT NULL,
    payment_method character varying(50),
    status character varying(50),
    note text,
    hold_note character varying(500),
    held_at timestamp without time zone,
    created_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT invoices_pkey PRIMARY KEY (invoice_id),
    CONSTRAINT invoices_order_id_fkey FOREIGN KEY (order_id) REFERENCES orders (order_id) ON DELETE CASCADE,
    CONSTRAINT invoices_user_id_fkey FOREIGN KEY (user_id) REFERENCES users (user_id)
);


CREATE TABLE return_orders (
    return_order_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    return_code character varying(50),
    order_id uuid NOT NULL,
    user_id uuid NOT NULL,
    refund_amount numeric(15,2) NOT NULL,
    refund_method character varying(50),
    return_type character varying(50),
    status character varying(50),
    note text,
    created_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT return_orders_pkey PRIMARY KEY (return_order_id),
    CONSTRAINT return_orders_order_id_fkey FOREIGN KEY (order_id) REFERENCES orders (order_id),
    CONSTRAINT return_orders_user_id_fkey FOREIGN KEY (user_id) REFERENCES users (user_id)
);


CREATE TABLE branch_products (
    branch_product_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    branch_id uuid NOT NULL,
    product_id uuid NOT NULL,
    cost_price numeric(15,2) NOT NULL,
    sell_price numeric(15,2) NOT NULL,
    actual_stock numeric(10,2) NOT NULL DEFAULT 0.0,
    reserved_stock numeric(10,2) NOT NULL DEFAULT 0.0,
    available_stock numeric(10,2) NOT NULL DEFAULT 0.0,
    minimum_stock numeric(10,2) DEFAULT 10.0,
    maximum_stock numeric(10,2) DEFAULT 100.0,
    shelf_location character varying(100),
    is_active smallint DEFAULT 1,
    direct_sale smallint DEFAULT 1,
    estimated_out_at timestamp without time zone,
    CONSTRAINT branch_products_pkey PRIMARY KEY (branch_product_id),
    CONSTRAINT branch_products_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES branches (branch_id),
    CONSTRAINT branch_products_product_id_fkey FOREIGN KEY (product_id) REFERENCES products (product_id)
);


CREATE TABLE inventory_transfer_details (
    detail_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    transfer_id uuid NOT NULL,
    product_id uuid NOT NULL,
    transfer_quantity integer NOT NULL,
    CONSTRAINT inventory_transfer_details_pkey PRIMARY KEY (detail_id),
    CONSTRAINT "FK_inventory_transfer_details_inventory_transfer_tickets_trans~" FOREIGN KEY (transfer_id) REFERENCES inventory_transfer_tickets (transfer_id) ON DELETE CASCADE,
    CONSTRAINT "FK_inventory_transfer_details_products_product_id" FOREIGN KEY (product_id) REFERENCES products (product_id)
);


CREATE TABLE media_resources (
    media_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    product_id uuid,
    post_id uuid,
    media_url text NOT NULL,
    media_type character varying(20) NOT NULL,
    is_primary smallint DEFAULT 0,
    created_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT media_resources_pkey PRIMARY KEY (media_id),
    CONSTRAINT media_resources_product_id_fkey FOREIGN KEY (product_id) REFERENCES products (product_id) ON DELETE CASCADE
);


CREATE TABLE product_attributes (
    attribute_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    product_id uuid NOT NULL,
    attribute_name character varying(100) NOT NULL,
    attribute_value character varying(255) NOT NULL,
    CONSTRAINT product_attributes_pkey PRIMARY KEY (attribute_id),
    CONSTRAINT product_attributes_product_id_fkey FOREIGN KEY (product_id) REFERENCES products (product_id) ON DELETE CASCADE
);


CREATE TABLE product_conversion_units (
    conversion_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    product_id uuid NOT NULL,
    unit_name character varying(20) NOT NULL,
    convert_value numeric(10,2) NOT NULL,
    price numeric(15,2) NOT NULL,
    direct_sale smallint DEFAULT 1,
    CONSTRAINT product_conversion_units_pkey PRIMARY KEY (conversion_id),
    CONSTRAINT product_conversion_units_product_id_fkey FOREIGN KEY (product_id) REFERENCES products (product_id) ON DELETE CASCADE
);


CREATE TABLE purchase_order_items (
    po_item_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    purchase_order_id uuid NOT NULL,
    product_id uuid NOT NULL,
    quantity numeric(10,2) NOT NULL,
    unit_price numeric(15,2) NOT NULL,
    CONSTRAINT purchase_order_items_pkey PRIMARY KEY (po_item_id),
    CONSTRAINT purchase_order_items_product_id_fkey FOREIGN KEY (product_id) REFERENCES products (product_id),
    CONSTRAINT purchase_order_items_purchase_order_id_fkey FOREIGN KEY (purchase_order_id) REFERENCES purchase_orders (purchase_order_id) ON DELETE CASCADE
);


CREATE TABLE "SupplierPayments" (
    "PaymentId" uuid NOT NULL,
    "SupplierId" uuid NOT NULL,
    "PurchaseOrderId" uuid,
    "Amount" numeric(18,2) NOT NULL,
    "PaymentDate" timestamp without time zone NOT NULL,
    "PaymentMethod" character varying(50) NOT NULL,
    "ReferenceCode" character varying(100),
    "Note" text,
    "RecordedBy" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "IsDeleted" smallint NOT NULL DEFAULT 0,
    created_by uuid,
    CONSTRAINT "PK_SupplierPayments" PRIMARY KEY ("PaymentId"),
    CONSTRAINT "FK_SupplierPayments_purchase_orders_PurchaseOrderId" FOREIGN KEY ("PurchaseOrderId") REFERENCES purchase_orders (purchase_order_id) ON DELETE SET NULL,
    CONSTRAINT "FK_SupplierPayments_suppliers_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES suppliers (supplier_id),
    CONSTRAINT "FK_SupplierPayments_users_RecordedBy" FOREIGN KEY ("RecordedBy") REFERENCES users (user_id) ON DELETE CASCADE,
    CONSTRAINT supplier_payments_created_by_fkey FOREIGN KEY (created_by) REFERENCES users (user_id) ON DELETE SET NULL
);


CREATE TABLE inventory_check_tickets (
    ticket_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    ticket_code character varying(50) NOT NULL,
    created_at timestamp without time zone NOT NULL,
    status character varying(50) NOT NULL,
    branch_id uuid NOT NULL,
    created_by_user_id uuid NOT NULL,
    filled_by_user_id uuid,
    filled_at timestamp without time zone,
    approved_by_user_id uuid,
    approved_at timestamp without time zone,
    cancelled_by_user_id uuid,
    cancelled_at timestamp without time zone,
    cancelled_reason character varying(1000),
    assignee_user_id uuid,
    linked_stock_ticket_id uuid,
    notes character varying(1000),
    recount_number integer NOT NULL DEFAULT 0,
    recount_requested_at timestamp without time zone,
    recount_requested_by_user_id uuid,
    recount_reason character varying(1000),
    CONSTRAINT inventory_check_tickets_pkey PRIMARY KEY (ticket_id),
    CONSTRAINT inventory_check_tickets_approved_by_user_id_fkey FOREIGN KEY (approved_by_user_id) REFERENCES users (user_id) ON DELETE SET NULL,
    CONSTRAINT inventory_check_tickets_assignee_user_id_fkey FOREIGN KEY (assignee_user_id) REFERENCES users (user_id) ON DELETE SET NULL,
    CONSTRAINT inventory_check_tickets_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES branches (branch_id),
    CONSTRAINT inventory_check_tickets_cancelled_by_user_id_fkey FOREIGN KEY (cancelled_by_user_id) REFERENCES users (user_id) ON DELETE SET NULL,
    CONSTRAINT inventory_check_tickets_created_by_user_id_fkey FOREIGN KEY (created_by_user_id) REFERENCES users (user_id),
    CONSTRAINT inventory_check_tickets_filled_by_user_id_fkey FOREIGN KEY (filled_by_user_id) REFERENCES users (user_id) ON DELETE SET NULL,
    CONSTRAINT inventory_check_tickets_linked_stock_ticket_id_fkey FOREIGN KEY (linked_stock_ticket_id) REFERENCES stock_tickets (stock_ticket_id) ON DELETE SET NULL,
    CONSTRAINT inventory_check_tickets_recount_requested_by_user_id_fkey FOREIGN KEY (recount_requested_by_user_id) REFERENCES users (user_id) ON DELETE SET NULL
);


CREATE TABLE stock_ticket_receipts (
    receipt_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    receipt_code character varying(50) NOT NULL,
    stock_ticket_id uuid NOT NULL,
    html_content text,
    printed_at timestamp without time zone,
    created_at timestamp without time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT stock_ticket_receipts_pkey PRIMARY KEY (receipt_id),
    CONSTRAINT "FK_stock_ticket_receipts_stock_tickets_stock_ticket_id" FOREIGN KEY (stock_ticket_id) REFERENCES stock_tickets (stock_ticket_id) ON DELETE CASCADE
);


CREATE TABLE payments (
    payment_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    invoice_id uuid,
    method character varying(50) NOT NULL,
    amount numeric(15,2) NOT NULL,
    status character varying(50) NOT NULL DEFAULT 'Pending',
    cash_received numeric(15,2),
    change_amount numeric(15,2),
    viet_qr_string text,
    qr_expired_at timestamp without time zone,
    paid_at timestamp without time zone,
    created_at timestamp without time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    branch_id uuid,
    CONSTRAINT payments_pkey PRIMARY KEY (payment_id),
    CONSTRAINT payments_invoice_id_fkey FOREIGN KEY (invoice_id) REFERENCES invoices (invoice_id)
);


CREATE TABLE receipts (
    receipt_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    receipt_code character varying(50) NOT NULL,
    invoice_id uuid NOT NULL,
    html_content text,
    printed_at timestamp without time zone,
    created_at timestamp without time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT receipts_pkey PRIMARY KEY (receipt_id),
    CONSTRAINT receipts_invoice_id_fkey FOREIGN KEY (invoice_id) REFERENCES invoices (invoice_id) ON DELETE CASCADE
);


CREATE TABLE refund_records (
    refund_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    refund_code character varying(50) NOT NULL,
    order_id uuid NOT NULL,
    invoice_id uuid,
    customer_id uuid NOT NULL,
    user_id uuid NOT NULL,
    refund_amount numeric(15,2) NOT NULL,
    refund_method character varying(50),
    status character varying(50) NOT NULL DEFAULT 'PENDING',
    reason text,
    processed_at timestamp without time zone,
    created_at timestamp without time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT refund_records_pkey PRIMARY KEY (refund_id),
    CONSTRAINT refund_records_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES customers (customer_id) ON DELETE CASCADE,
    CONSTRAINT refund_records_invoice_id_fkey FOREIGN KEY (invoice_id) REFERENCES invoices (invoice_id),
    CONSTRAINT refund_records_order_id_fkey FOREIGN KEY (order_id) REFERENCES orders (order_id) ON DELETE CASCADE,
    CONSTRAINT refund_records_user_id_fkey FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE
);


CREATE TABLE invoice_items (
    invoice_item_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    invoice_id uuid NOT NULL,
    branch_product_id uuid NOT NULL,
    quantity numeric(10,2) NOT NULL,
    sell_price numeric(15,2) NOT NULL,
    "SelectedUnit" text,
    "ConvertValue" numeric,
    CONSTRAINT invoice_items_pkey PRIMARY KEY (invoice_item_id),
    CONSTRAINT invoice_items_branch_product_id_fkey FOREIGN KEY (branch_product_id) REFERENCES branch_products (branch_product_id),
    CONSTRAINT invoice_items_invoice_id_fkey FOREIGN KEY (invoice_id) REFERENCES invoices (invoice_id) ON DELETE CASCADE
);


CREATE TABLE order_items (
    order_item_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    order_id uuid NOT NULL,
    branch_product_id uuid NOT NULL,
    quantity numeric(10,2) NOT NULL,
    sell_price numeric(15,2) NOT NULL,
    CONSTRAINT order_items_pkey PRIMARY KEY (order_item_id),
    CONSTRAINT order_items_branch_product_id_fkey FOREIGN KEY (branch_product_id) REFERENCES branch_products (branch_product_id),
    CONSTRAINT order_items_order_id_fkey FOREIGN KEY (order_id) REFERENCES orders (order_id) ON DELETE CASCADE
);


CREATE TABLE return_items (
    return_item_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    return_order_id uuid NOT NULL,
    branch_product_id uuid NOT NULL,
    quantity numeric(10,2) NOT NULL,
    refund_amount numeric(15,2),
    reason character varying(100),
    CONSTRAINT return_items_pkey PRIMARY KEY (return_item_id),
    CONSTRAINT return_items_branch_product_id_fkey FOREIGN KEY (branch_product_id) REFERENCES branch_products (branch_product_id),
    CONSTRAINT return_items_return_order_id_fkey FOREIGN KEY (return_order_id) REFERENCES return_orders (return_order_id) ON DELETE CASCADE
);


CREATE TABLE stock_ticket_items (
    ticket_item_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    stock_ticket_id uuid NOT NULL,
    branch_product_id uuid NOT NULL,
    system_quantity numeric(10,2) NOT NULL,
    actual_quantity numeric(10,2) NOT NULL,
    discrepancy numeric(10,2) NOT NULL,
    "UnitPrice" numeric,
    CONSTRAINT stock_ticket_items_pkey PRIMARY KEY (ticket_item_id),
    CONSTRAINT stock_ticket_items_branch_product_id_fkey FOREIGN KEY (branch_product_id) REFERENCES branch_products (branch_product_id),
    CONSTRAINT stock_ticket_items_stock_ticket_id_fkey FOREIGN KEY (stock_ticket_id) REFERENCES stock_tickets (stock_ticket_id) ON DELETE CASCADE
);


CREATE TABLE inventory_check_details (
    detail_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    ticket_id uuid NOT NULL,
    product_id uuid NOT NULL,
    system_quantity integer NOT NULL,
    actual_quantity integer NOT NULL,
    discrepancy integer NOT NULL,
    is_counted boolean NOT NULL,
    reason_note character varying(1000),
    counted_by_user_id uuid,
    counted_at timestamp without time zone,
    CONSTRAINT inventory_check_details_pkey PRIMARY KEY (detail_id),
    CONSTRAINT "FK_inventory_check_details_inventory_check_tickets_ticket_id" FOREIGN KEY (ticket_id) REFERENCES inventory_check_tickets (ticket_id) ON DELETE CASCADE,
    CONSTRAINT "FK_inventory_check_details_products_product_id" FOREIGN KEY (product_id) REFERENCES products (product_id),
    CONSTRAINT inventory_check_details_counted_by_user_id_fkey FOREIGN KEY (counted_by_user_id) REFERENCES users (user_id) ON DELETE SET NULL
);


CREATE TABLE notifications (
    notification_id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    type character varying(50) NOT NULL,
    recipient_user_id uuid,
    branch_id uuid NOT NULL,
    inventory_check_id uuid,
    reference_id uuid,
    reference_type character varying(100),
    triggered_by_user_id uuid NOT NULL,
    message text NOT NULL,
    is_read boolean NOT NULL DEFAULT FALSE,
    created_at timestamp without time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT notifications_pkey PRIMARY KEY (notification_id),
    CONSTRAINT notifications_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES branches (branch_id),
    CONSTRAINT notifications_inventory_check_id_fkey FOREIGN KEY (inventory_check_id) REFERENCES inventory_check_tickets (ticket_id) ON DELETE SET NULL,
    CONSTRAINT notifications_recipient_user_id_fkey FOREIGN KEY (recipient_user_id) REFERENCES users (user_id) ON DELETE SET NULL,
    CONSTRAINT notifications_triggered_by_user_id_fkey FOREIGN KEY (triggered_by_user_id) REFERENCES users (user_id)
);


CREATE INDEX "IX_branch_products_product_id" ON branch_products (product_id);


CREATE UNIQUE INDEX uk_branch_product ON branch_products (branch_id, product_id);


CREATE INDEX "IX_branches_manager_user_id" ON branches (manager_user_id);


CREATE UNIQUE INDEX customers_phone_unique ON customers (phone_number);


CREATE INDEX "IX_customers_BranchId" ON customers ("BranchId");


CREATE INDEX "IX_customers_CreatedByUserUserId" ON customers ("CreatedByUserUserId");


CREATE UNIQUE INDEX expense_categories_category_name_key ON expense_categories (category_name);


CREATE INDEX "IX_expense_categories_BranchId" ON expense_categories ("BranchId");


CREATE INDEX "IX_expense_categories_created_by" ON expense_categories (created_by);


CREATE UNIQUE INDEX expense_vouchers_voucher_code_key ON expense_vouchers (voucher_code);


CREATE INDEX ix_expense_vouchers_branch_id ON expense_vouchers (branch_id);


CREATE INDEX "IX_expense_vouchers_cancelled_by" ON expense_vouchers (cancelled_by);


CREATE INDEX ix_expense_vouchers_category_id ON expense_vouchers (category_id);


CREATE INDEX "IX_expense_vouchers_confirmed_by" ON expense_vouchers (confirmed_by);


CREATE INDEX "IX_expense_vouchers_created_by" ON expense_vouchers (created_by);


CREATE INDEX ix_expense_vouchers_status ON expense_vouchers (status);


CREATE INDEX "IX_expense_vouchers_supplier_id" ON expense_vouchers (supplier_id);


CREATE INDEX "IX_inventory_check_details_counted_by_user_id" ON inventory_check_details (counted_by_user_id);


CREATE INDEX "IX_inventory_check_details_product_id" ON inventory_check_details (product_id);


CREATE INDEX "IX_inventory_check_details_ticket_id" ON inventory_check_details (ticket_id);


CREATE INDEX "IX_inventory_check_tickets_approved_by_user_id" ON inventory_check_tickets (approved_by_user_id);


CREATE INDEX "IX_inventory_check_tickets_assignee_user_id" ON inventory_check_tickets (assignee_user_id);


CREATE INDEX "IX_inventory_check_tickets_branch_id" ON inventory_check_tickets (branch_id);


CREATE INDEX "IX_inventory_check_tickets_cancelled_by_user_id" ON inventory_check_tickets (cancelled_by_user_id);


CREATE INDEX "IX_inventory_check_tickets_created_by_user_id" ON inventory_check_tickets (created_by_user_id);


CREATE INDEX "IX_inventory_check_tickets_filled_by_user_id" ON inventory_check_tickets (filled_by_user_id);


CREATE INDEX "IX_inventory_check_tickets_linked_stock_ticket_id" ON inventory_check_tickets (linked_stock_ticket_id);


CREATE INDEX "IX_inventory_check_tickets_recount_requested_by_user_id" ON inventory_check_tickets (recount_requested_by_user_id);


CREATE UNIQUE INDEX uk_inventory_check_ticket_code ON inventory_check_tickets (ticket_code);


CREATE INDEX "IX_inventory_transfer_details_product_id" ON inventory_transfer_details (product_id);


CREATE INDEX "IX_inventory_transfer_details_transfer_id" ON inventory_transfer_details (transfer_id);


CREATE UNIQUE INDEX uk_inventory_transfer_code ON inventory_transfer_tickets (transfer_code);


CREATE INDEX "IX_invoice_items_branch_product_id" ON invoice_items (branch_product_id);


CREATE INDEX "IX_invoice_items_invoice_id" ON invoice_items (invoice_id);


CREATE INDEX "IX_invoice_print_templates_branch_id" ON invoice_print_templates (branch_id);


CREATE UNIQUE INDEX invoices_invoice_code_key ON invoices (invoice_code);


CREATE UNIQUE INDEX "IX_invoices_order_id" ON invoices (order_id);


CREATE INDEX "IX_invoices_user_id" ON invoices (user_id);


CREATE INDEX "IX_media_resources_product_id" ON media_resources (product_id);


CREATE INDEX "IX_notifications_branch_id" ON notifications (branch_id);


CREATE INDEX "IX_notifications_inventory_check_id" ON notifications (inventory_check_id);


CREATE INDEX "IX_notifications_recipient_user_id" ON notifications (recipient_user_id);


CREATE INDEX "IX_notifications_triggered_by_user_id" ON notifications (triggered_by_user_id);


CREATE UNIQUE INDEX ix_order_discount_tiers_branch_min_order ON order_discount_tiers (branch_id, min_order_value);


CREATE INDEX "IX_order_items_branch_product_id" ON order_items (branch_product_id);


CREATE INDEX "IX_order_items_order_id" ON order_items (order_id);


CREATE INDEX "IX_orders_branch_id" ON orders (branch_id);


CREATE INDEX "IX_orders_customer_id" ON orders (customer_id);


CREATE INDEX "IX_orders_user_id" ON orders (user_id);


CREATE UNIQUE INDEX orders_order_code_key ON orders (order_code);


CREATE UNIQUE INDEX ix_password_reset_tokens_token_hash ON password_reset_tokens (token_hash);


CREATE INDEX "IX_password_reset_tokens_user_id" ON password_reset_tokens (user_id);


CREATE INDEX "IX_payments_invoice_id" ON payments (invoice_id);


CREATE UNIQUE INDEX permissions_permission_code_key ON permissions (permission_code);


CREATE UNIQUE INDEX ix_product_attribute_types_type_name ON product_attribute_types (type_name);


CREATE INDEX "IX_product_attributes_product_id" ON product_attributes (product_id);


CREATE INDEX "IX_product_conversion_units_product_id" ON product_conversion_units (product_id);


CREATE UNIQUE INDEX ix_product_locations_location_name ON product_locations (location_name);


CREATE INDEX "IX_product_taxonomies_branch_id" ON product_taxonomies (branch_id);


CREATE INDEX "IX_products_BrandTaxonomyId" ON products ("BrandTaxonomyId");


CREATE INDEX "IX_products_CategoryTaxonomyId" ON products ("CategoryTaxonomyId");


CREATE INDEX "IX_products_created_by" ON products (created_by);


CREATE INDEX "IX_products_supplier_id" ON products (supplier_id);


CREATE INDEX "IX_products_updated_by" ON products (updated_by);


CREATE UNIQUE INDEX products_product_code_key ON products (product_code);


CREATE INDEX "IX_purchase_order_items_product_id" ON purchase_order_items (product_id);


CREATE INDEX "IX_purchase_order_items_purchase_order_id" ON purchase_order_items (purchase_order_id);


CREATE INDEX "IX_purchase_orders_approved_by" ON purchase_orders (approved_by);


CREATE INDEX "IX_purchase_orders_supplier_id" ON purchase_orders (supplier_id);


CREATE INDEX "IX_purchase_orders_user_id" ON purchase_orders (user_id);


CREATE UNIQUE INDEX purchase_orders_order_code_key ON purchase_orders (order_code);


CREATE UNIQUE INDEX ix_receipts_code ON receipts (receipt_code);


CREATE INDEX "IX_receipts_invoice_id" ON receipts (invoice_id);


CREATE UNIQUE INDEX ix_refund_records_code ON refund_records (refund_code);


CREATE INDEX "IX_refund_records_customer_id" ON refund_records (customer_id);


CREATE INDEX "IX_refund_records_invoice_id" ON refund_records (invoice_id);


CREATE INDEX "IX_refund_records_order_id" ON refund_records (order_id);


CREATE INDEX "IX_refund_records_user_id" ON refund_records (user_id);


CREATE INDEX "IX_return_items_branch_product_id" ON return_items (branch_product_id);


CREATE INDEX "IX_return_items_return_order_id" ON return_items (return_order_id);


CREATE INDEX "IX_return_orders_order_id" ON return_orders (order_id);


CREATE INDEX "IX_return_orders_user_id" ON return_orders (user_id);


CREATE UNIQUE INDEX return_orders_return_code_key ON return_orders (return_code);


CREATE INDEX "IX_role_permissions_permission_id" ON role_permissions (permission_id);


CREATE UNIQUE INDEX roles_role_name_key ON roles (role_name);


CREATE INDEX "IX_shifts_branch_id" ON shifts (branch_id);


CREATE UNIQUE INDEX ix_shifts_code ON shifts (shift_code);


CREATE INDEX "IX_shifts_user_id" ON shifts (user_id);


CREATE INDEX "IX_staff_assignments_branch_id" ON staff_assignments (branch_id);


CREATE INDEX "IX_staff_assignments_user_id" ON staff_assignments (user_id);


CREATE UNIQUE INDEX ix_stock_print_templates_branch_ticket ON stock_print_templates (branch_id, ticket_type);


CREATE INDEX "IX_stock_ticket_items_branch_product_id" ON stock_ticket_items (branch_product_id);


CREATE INDEX "IX_stock_ticket_items_stock_ticket_id" ON stock_ticket_items (stock_ticket_id);


CREATE UNIQUE INDEX ix_stock_ticket_receipts_code ON stock_ticket_receipts (receipt_code);


CREATE INDEX "IX_stock_ticket_receipts_stock_ticket_id" ON stock_ticket_receipts (stock_ticket_id);


CREATE INDEX "IX_stock_tickets_branch_id" ON stock_tickets (branch_id);


CREATE INDEX "IX_stock_tickets_cancelled_by" ON stock_tickets (cancelled_by);


CREATE INDEX "IX_stock_tickets_confirmed_by" ON stock_tickets (confirmed_by);


CREATE INDEX "IX_stock_tickets_supplier_id" ON stock_tickets (supplier_id);


CREATE INDEX "IX_stock_tickets_user_id" ON stock_tickets (user_id);


CREATE UNIQUE INDEX stock_tickets_ticket_code_key ON stock_tickets (ticket_code);


CREATE INDEX "IX_SupplierPayments_created_by" ON "SupplierPayments" (created_by);


CREATE INDEX "IX_SupplierPayments_PurchaseOrderId" ON "SupplierPayments" ("PurchaseOrderId");


CREATE INDEX "IX_SupplierPayments_RecordedBy" ON "SupplierPayments" ("RecordedBy");


CREATE INDEX "IX_SupplierPayments_SupplierId" ON "SupplierPayments" ("SupplierId");


CREATE INDEX "IX_suppliers_BranchId" ON suppliers ("BranchId");


CREATE INDEX "IX_suppliers_created_by" ON suppliers (created_by);


CREATE INDEX "IX_suppliers_updated_by" ON suppliers (updated_by);


CREATE INDEX "IX_system_configs_branch_id" ON system_configs (branch_id);


CREATE INDEX "IX_system_logs_user_id" ON system_logs (user_id);


CREATE INDEX "IX_system_notifications_created_by" ON system_notifications (created_by);


CREATE INDEX "IX_user_permissions_permission_id" ON user_permissions (permission_id);


CREATE INDEX "IX_user_permissions_user_id" ON user_permissions (user_id);


CREATE INDEX "IX_user_roles_role_id" ON user_roles (role_id);


CREATE INDEX "IX_users_created_by" ON users (created_by);


CREATE UNIQUE INDEX users_email_unique ON users (email);


CREATE UNIQUE INDEX users_phone_unique ON users (phone_number);


CREATE UNIQUE INDEX users_username_key ON users (username);


