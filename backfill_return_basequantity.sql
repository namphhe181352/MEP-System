-- ============================================================
-- BACKFILL: Tính lại return_items.conversion_rate / base_quantity
--           + warranty_tickets.conversion_rate / base_quantity
-- Nguồn sự thật: invoice_items.convert_value (dòng hóa đơn gốc).
--
-- VẤN ĐỀ: Trước đây AddReturnItem tin tưởng req.ConversionRate do FE gửi
-- (thường thiếu -> mặc định 1) -> base_quantity = quantity, KHÔNG quy về
-- ĐVT cơ bản -> SL trong "Đơn chờ phân NCC" bị lệch hoàn toàn.
--
-- CHIẾN LƯỢC AN TOÀN:
--   Mỗi return_item join sang invoice_items gốc (cùng hóa đơn + cùng SP).
--   - Nếu chỉ có 1 dòng gốc cho SP đó  -> lấy convert_value của dòng đó.
--   - Nếu nhiều dòng (1 SP nhiều đơn vị trong 1 HĐ) nhưng có ĐÚNG 1 dòng
--     có quantity == return_item.quantity -> lấy dòng đó (khớp theo SL bán).
--   - Còn lại (nhiều dòng, không khớp duy nhất) -> BỎ QUA, liệt kê ở cuối
--     để xử lý thủ công (tránh đoán nhầm đơn vị).
--
-- CÁCH DÙNG:
--   1) Chạy khối DRY-RUN để xem trước (không sửa gì).
--   2) Chạy khối UPDATE (đã bọc trong transaction, có ROLLBACK dự phòng).
--   3) Chạy khối BÁO CÁO TH MỜ HỒ để xử lý tay nếu còn.
-- ============================================================


-- ============================================================
-- KHỐI 1: DRY-RUN — xem trước giá trị cũ vs mới + trạng thái match
-- ============================================================
WITH candidates AS (
    SELECT
        ri.return_item_id,
        ri.branch_product_id,
        ri.return_order_id,
        ri.quantity          AS ri_qty,
        ri.conversion_rate   AS old_rate,
        ri.base_quantity     AS old_base,
        ii.invoice_item_id   AS ii_id,
        ii.convert_value     AS ii_cv,
        ii.quantity          AS ii_qty,
        COUNT(*)             OVER (PARTITION BY ri.return_item_id) AS cand_count,
        SUM(CASE WHEN ii.quantity = ri.quantity THEN 1 ELSE 0 END)
            OVER (PARTITION BY ri.return_item_id) AS exact_count
    FROM return_items ri
    JOIN return_orders ro ON ro.return_order_id = ri.return_order_id
    JOIN invoices      inv ON inv.order_id = ro.order_id
    JOIN invoice_items ii  ON ii.invoice_id = inv.invoice_id
                           AND ii.branch_product_id = ri.branch_product_id
),
-- Chọn 1 dòng gốc / return_item khi KHÔNG mơ hồ:
--   cand_count = 1                       -> dòng duy nhất
--   exact_count = 1 AND ii_qty = ri_qty  -> dòng khớp SL bán duy nhất
chosen AS (
    SELECT
        c.return_item_id,
        c.ri_qty,
        c.old_rate,
        c.old_base,
        c.cand_count,
        c.exact_count,
        COALESCE(c.ii_cv, 1.0) AS new_rate,
        c.ri_qty * COALESCE(c.ii_cv, 1.0) AS new_base
    FROM candidates c
    WHERE (c.cand_count = 1)
       OR (c.exact_count = 1 AND c.ii_qty = c.ri_qty)
)
SELECT
    ch.return_item_id,
    ri_qty,
    old_rate,
    new_rate,
    old_base,
    new_base,
    CASE
        WHEN old_rate IS DISTINCT FROM new_rate OR old_base IS DISTINCT FROM new_base
        THEN 'WILL UPDATE'
        ELSE 'no change'
    END AS action,
    cand_count,
    exact_count
FROM chosen ch
ORDER BY ch.return_item_id;


-- ============================================================
-- KHỐI 2: UPDATE — bọc trong transaction. Đổi ROLLBACK -> COMMIT khi thấy đúng.
-- ============================================================
BEGIN;

-- 2a. Cập nhật return_items
WITH candidates AS (
    SELECT
        ri.return_item_id,
        ri.quantity      AS ri_qty,
        ii.convert_value AS ii_cv,
        ii.quantity      AS ii_qty,
        COUNT(*)         OVER (PARTITION BY ri.return_item_id) AS cand_count,
        SUM(CASE WHEN ii.quantity = ri.quantity THEN 1 ELSE 0 END)
            OVER (PARTITION BY ri.return_item_id) AS exact_count
    FROM return_items ri
    JOIN return_orders ro ON ro.return_order_id = ri.return_order_id
    JOIN invoices      inv ON inv.order_id = ro.order_id
    JOIN invoice_items ii  ON ii.invoice_id = inv.invoice_id
                           AND ii.branch_product_id = ri.branch_product_id
),
chosen AS (
    SELECT
        c.return_item_id,
        COALESCE(c.ii_cv, 1.0) AS new_rate
    FROM candidates c
    WHERE (c.cand_count = 1)
       OR (c.exact_count = 1 AND c.ii_qty = c.ri_qty)
)
UPDATE return_items ri
SET
    conversion_rate = ch.new_rate,
    base_quantity   = ri.quantity * ch.new_rate
FROM chosen ch
WHERE ri.return_item_id = ch.return_item_id
  AND (ri.conversion_rate IS DISTINCT FROM ch.new_rate
       OR ri.base_quantity IS DISTINCT FROM ri.quantity * ch.new_rate);

-- 2b. Lan truyền sang warranty_tickets (cùng conversion_rate + base_quantity
--     của return_item đã sửa). warranty_tickets.quantity đã đúng (= SL bán).
UPDATE warranty_tickets wt
SET
    conversion_rate = ri.conversion_rate,
    base_quantity   = ri.base_quantity
FROM return_items ri
WHERE wt.return_item_id = ri.return_item_id
  AND (wt.conversion_rate IS DISTINCT FROM ri.conversion_rate
       OR wt.base_quantity IS DISTINCT FROM ri.base_quantity);

-- 2c. Verify kết quả trước khi commit
SELECT
    ri.return_item_id,
    ro.return_code,
    ri.branch_product_id,
    ri.quantity       AS ri_qty,
    ri.conversion_rate AS new_rate,
    ri.base_quantity   AS new_base,
    wt.warranty_ticket_id,
    wt.base_quantity   AS wt_base
FROM return_items ri
JOIN return_orders ro ON ro.return_order_id = ri.return_order_id
LEFT JOIN warranty_tickets wt ON wt.return_item_id = ri.return_item_id
ORDER BY ro.return_code, ri.return_item_id;

-- >>> ĐỔI DÒNG DƯỚI THÀNH 'COMMIT;' KHI KẾT QUẢ 2c ĐÚNG <<<
ROLLBACK;


-- ============================================================
-- KHỐI 3: BÁO CÁO TH MỜ HỒ — return_item KHÔNG tự fix được
-- (1 SP có nhiều đơn vị trong 1 hóa đơn MÀ không khớp SL duy nhất).
-- Xử lý thủ công: gán conversion_rate đúng cho từng return_item_id.
-- ============================================================
WITH candidates AS (
    SELECT
        ri.return_item_id,
        ro.return_code,
        ri.branch_product_id,
        ri.quantity          AS ri_qty,
        ri.conversion_rate   AS old_rate,
        ri.base_quantity     AS old_base,
        ii.invoice_item_id   AS ii_id,
        ii.convert_value     AS ii_cv,
        ii.quantity          AS ii_qty,
        COUNT(*)             OVER (PARTITION BY ri.return_item_id) AS cand_count,
        SUM(CASE WHEN ii.quantity = ri.quantity THEN 1 ELSE 0 END)
            OVER (PARTITION BY ri.return_item_id) AS exact_count
    FROM return_items ri
    JOIN return_orders ro ON ro.return_order_id = ri.return_order_id
    JOIN invoices      inv ON inv.order_id = ro.order_id
    JOIN invoice_items ii  ON ii.invoice_id = inv.invoice_id
                           AND ii.branch_product_id = ri.branch_product_id
),
not_chosen AS (
    -- return_item KHÔNG tự fix được: nhiều dòng gốc (cand_count > 1)
    -- MÀ không có đúng 1 dòng khớp SL bán (exact_count = 0 hoặc > 1).
    SELECT return_item_id
    FROM candidates
    GROUP BY return_item_id
    HAVING MAX(cand_count) > 1
       AND (MAX(exact_count) = 0 OR MAX(exact_count) > 1)
)
SELECT
    c.return_item_id,
    c.return_code,
    c.branch_product_id,
    c.ri_qty,
    c.old_rate,
    c.old_base,
    c.ii_id,
    c.ii_qty,
    c.ii_cv,
    c.cand_count,
    c.exact_count
FROM candidates c
JOIN not_chosen nc ON nc.return_item_id = c.return_item_id
ORDER BY c.return_code, c.return_item_id, c.ii_id;

-- Sau khi xem KHỐI 3, nếu muốn fix tay 1 dòng (ví dụ dòng gốc ii_id có convert_value = 20):
-- UPDATE return_items SET conversion_rate = 20, base_quantity = quantity * 20
--  WHERE return_item_id = '<id>';
-- UPDATE warranty_tickets SET conversion_rate = 20, base_quantity = (SELECT quantity FROM return_items WHERE return_item_id = '<id>') * 20
--  WHERE return_item_id = '<id>';
