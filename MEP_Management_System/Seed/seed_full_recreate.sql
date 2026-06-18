-- Full seed for the current MEP schema. This file is executed by the EF Core seed runner.

BEGIN;

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

INSERT INTO permissions (permission_id, permission_code, permission_name) VALUES
  ('00000000-0000-0000-0000-000000000001', 'STOCK_VIEW', 'Xem tồn kho chi nhánh'),
  ('00000000-0000-0000-0000-000000000002', 'STOCK_MANAGE', 'Nhập kho và điều chỉnh kho'),
  ('00000000-0000-0000-0000-000000000003', 'POS_SALE', 'Thực hiện bán hàng tại quầy'),
  ('00000000-0000-0000-0000-000000000004', 'PO_APPROVE', 'Phê duyệt đơn mua hàng Supplier'),
  ('00000000-0000-0000-0000-000000000005', 'COMMUNITY_MOD', 'Kiểm duyệt bài viết diễn đàn')
ON CONFLICT (permission_id) DO NOTHING;

INSERT INTO roles (role_id, role_name, description, is_system) VALUES
  ('10000000-0000-0000-0000-000000000001', 'Shop Owner', 'Chủ chuỗi cửa hàng, toàn quyền', 1),
  ('10000000-0000-0000-0000-000000000002', 'Sales Staff', 'Nhân viên bán hàng', 0),
  ('10000000-0000-0000-0000-000000000003', 'Inventory Staff', 'Nhân viên quản lý kho', 0)
ON CONFLICT (role_id) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id) VALUES
  ('10000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001'),
  ('10000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002'),
  ('10000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000003'),
  ('10000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000004'),
  ('10000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000005'),
  ('10000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001'),
  ('10000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000003'),
  ('10000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001'),
  ('10000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000002')
ON CONFLICT DO NOTHING;

INSERT INTO users (user_id, username, password_hash, full_name, email, phone_number, is_active, created_at) VALUES
  ('20000000-0000-0000-0000-000000000001', 'owner_boss', 'hashed_password_123', 'Nguyen Thanh Boss', 'owner@example.com', '0900000001', 1, CURRENT_TIMESTAMP),
  ('20000000-0000-0000-0000-000000000002', 'sale_an', 'hashed_password_456', 'Tran Thi An (Sale)', 'sale@example.com', '0900000002', 1, CURRENT_TIMESTAMP),
  ('20000000-0000-0000-0000-000000000003', 'kho_binh', 'hashed_password_789', 'Le Van Binh (Kho)', 'inventory@example.com', '0900000003', 1, CURRENT_TIMESTAMP)
ON CONFLICT (user_id) DO NOTHING;

INSERT INTO user_roles (user_id, role_id) VALUES
  ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001'),
  ('20000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000002'),
  ('20000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000003')
ON CONFLICT DO NOTHING;

INSERT INTO branches (branch_id, branch_name, address) VALUES
  ('30000000-0000-0000-0000-000000000001', 'Chi nhanh Mien Bac', '123 Cầu Giấy, Hà Nội'),
  ('30000000-0000-0000-0000-000000000002', 'Chi nhanh Mien Nam', '456 Quận 1, TP. Hồ Chí Minh')
ON CONFLICT (branch_id) DO NOTHING;

INSERT INTO suppliers (supplier_id, supplier_name, contact_phone) VALUES
  ('60000000-0000-0000-0000-000000000001', 'Stanley Viet Nam', '0243999xxxx'),
  ('60000000-0000-0000-0000-000000000002', 'Nhà cung cấp Tổng hợp Kim khí - Điện nước', '0243999xxxx')
ON CONFLICT (supplier_id) DO NOTHING;

INSERT INTO products (product_id, product_code, product_name, barcode, base_unit, specification, item_type, brand_name, category_name, supplier_id, weight, weight_unit, sales_channel_linked, created_at) VALUES
  ('70000000-0000-0000-0000-000000000001', 'SP001', 'Búa sắt cán gỗ 500g', '893100000001', 'Cái', '500g', 'Công cụ cầm tay', 'Stanley', 'Dụng cụ', '60000000-0000-0000-0000-000000000001', 0.60, 'kg', 1, CURRENT_TIMESTAMP),
  ('70000000-0000-0000-0000-000000000002', 'SP002', 'Kìm cắt điện 8 inch', '893100000002', 'Cái', '8 inch', 'Công cụ cầm tay', 'Generic', 'Dụng cụ', '60000000-0000-0000-0000-000000000002', 0.30, 'kg', 1, CURRENT_TIMESTAMP)
ON CONFLICT (product_id) DO NOTHING;

INSERT INTO branch_products (branch_product_id, branch_id, product_id, cost_price, sell_price, actual_stock, reserved_stock, available_stock, minimum_stock, maximum_stock, shelf_location, is_active, direct_sale) VALUES
  ('90000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000001', 85000.00, 120000.00, 50, 5, 45, 10, 100, 'Ke A1', 1, 1),
  ('90000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000002', 65000.00, 95000.00, 5, 0, 5, 2, 20, NULL, 1, 1)
ON CONFLICT (branch_product_id) DO NOTHING;

INSERT INTO purchase_orders (purchase_order_id, supplier_id, user_id, total_amount, status, created_at) VALUES
  ('b0000000-0000-0000-0000-000000000001', '60000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000003', 8500000.00, 'APPROVED', CURRENT_TIMESTAMP)
ON CONFLICT (purchase_order_id) DO NOTHING;

INSERT INTO purchase_order_items (po_item_id, purchase_order_id, product_id, quantity, unit_price) VALUES
  ('b1000000-0000-0000-0000-000000000001', 'b0000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000001', 100, 85000.00)
ON CONFLICT (po_item_id) DO NOTHING;

INSERT INTO stock_tickets (stock_ticket_id, branch_id, user_id, created_at) VALUES
  ('c0000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000003', CURRENT_TIMESTAMP)
ON CONFLICT (stock_ticket_id) DO NOTHING;

INSERT INTO stock_ticket_items (ticket_item_id, stock_ticket_id, branch_product_id, system_quantity, actual_quantity, discrepancy) VALUES
  ('c1000000-0000-0000-0000-000000000001', 'c0000000-0000-0000-0000-000000000001', '90000000-0000-0000-0000-000000000001', 50, 49, -1)
ON CONFLICT (ticket_item_id) DO NOTHING;

INSERT INTO customers (customer_id, customer_name, phone_number, loyalty_points) VALUES
  ('d0000000-0000-0000-0000-000000000001', 'Anh Tran Khach Quen', '0912345678', 120)
ON CONFLICT (customer_id) DO NOTHING;

INSERT INTO orders (order_id, order_code, branch_id, customer_id, user_id, total_amount, status, created_at) VALUES
  ('a0000000-0000-0000-0000-000000000001', 'ORD-001', '30000000-0000-0000-0000-000000000001', 'd0000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000002', 240000.00, 'PAID', CURRENT_TIMESTAMP)
ON CONFLICT (order_id) DO NOTHING;

INSERT INTO order_items (order_item_id, order_id, branch_product_id, quantity, sell_price) VALUES
  ('a1000000-0000-0000-0000-000000000001', 'a0000000-0000-0000-0000-000000000001', '90000000-0000-0000-0000-000000000001', 2, 120000.00)
ON CONFLICT (order_item_id) DO NOTHING;

INSERT INTO invoices (invoice_id, order_id, user_id, total_amount, payment_method, created_at) VALUES
        ('e0000000-0000-0000-0000-000000000001', 'a0000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000002', 240000.00, 'CASH', CURRENT_TIMESTAMP)
ON CONFLICT (invoice_id) DO NOTHING;

INSERT INTO invoice_items (invoice_item_id, invoice_id, branch_product_id, quantity, sell_price) VALUES
  ('e1000000-0000-0000-0000-000000000001', 'e0000000-0000-0000-0000-000000000001', '90000000-0000-0000-0000-000000000001', 2, 120000.00)
ON CONFLICT (invoice_item_id) DO NOTHING;

INSERT INTO return_orders (return_order_id, order_id, user_id, refund_amount, created_at) VALUES
        ('f0000000-0000-0000-0000-000000000001', 'a0000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000002', 120000.00, CURRENT_TIMESTAMP)
ON CONFLICT (return_order_id) DO NOTHING;

INSERT INTO return_items (return_item_id, return_order_id, branch_product_id, quantity) VALUES
  ('f1000000-0000-0000-0000-000000000001', 'f0000000-0000-0000-0000-000000000001', '90000000-0000-0000-0000-000000000001', 1)
ON CONFLICT (return_item_id) DO NOTHING;

INSERT INTO community_categories (category_id, category_name, description) VALUES
  ('a1000000-0000-0000-0000-000000000001', 'Góc gom mua chung', 'Nội dung liên kết gom đơn')
ON CONFLICT (category_id) DO NOTHING;

INSERT INTO community_posts (post_id, category_id, user_id, title, content, created_at) VALUES
  ('a2000000-0000-0000-0000-000000000001', 'a1000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', 'Gom đơn nhập búa Stanley', 'Minh đang cần nhập sỉ búa Stanley từ xưởng.', CURRENT_TIMESTAMP)
ON CONFLICT (post_id) DO NOTHING;

INSERT INTO comments (comment_id, post_id, user_id, comment_text, created_at) VALUES
  ('a3000000-0000-0000-0000-000000000001', 'a2000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000002', 'Em đăng ký chung 50 thùng nhé!', CURRENT_TIMESTAMP)
ON CONFLICT (comment_id) DO NOTHING;

INSERT INTO violation_reports (report_id, post_id, comment_id, user_id, reason, status, created_at) VALUES
  ('a4000000-0000-0000-0000-000000000001', NULL, 'a3000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000003', 'Spam quảng cáo sai sự thật', 'PENDING', CURRENT_TIMESTAMP)
ON CONFLICT (report_id) DO NOTHING;

INSERT INTO media_resources (media_id, product_id, post_id, media_url, media_type, is_primary, created_at) VALUES
  (uuid_generate_v4(), '70000000-0000-0000-0000-000000000001', NULL, 'https://storage.mep.com/products/bua-500g-mat-truoc.jpg', 'IMAGE', 1, CURRENT_TIMESTAMP)
ON CONFLICT DO NOTHING;

COMMIT;
