-- ============================================================
-- Migration: Add Supplier Rating
-- Ngày: 2026-06-21
-- Mục đích:
--   1) Mở rộng bảng suppliers (thêm contact_person, tax_code, group_name,
--      bank_account, bank_name, website, notes, average_rating,
--      rating_count, last_rated_at).
--   2) Tạo bảng mới supplier_ratings (lưu lịch sử đánh giá NCC).
--
-- Cách chạy:
--   psql -h <host> -U postgres -d "M.E.P Database" -f AddSupplierRating.sql
-- Hoặc mở pgAdmin -> Query Tool -> paste toàn bộ file này -> Execute.
--
-- Rollback: xem block "ROLLBACK SCRIPT" ở cuối file.
-- ============================================================

BEGIN;

-- 1) Mở rộng bảng suppliers
ALTER TABLE suppliers
    ADD COLUMN IF NOT EXISTS contact_person   VARCHAR(100),
    ADD COLUMN IF NOT EXISTS tax_code         VARCHAR(50),
    ADD COLUMN IF NOT EXISTS group_name       VARCHAR(50),
    ADD COLUMN IF NOT EXISTS bank_account     VARCHAR(50),
    ADD COLUMN IF NOT EXISTS bank_name        VARCHAR(100),
    ADD COLUMN IF NOT EXISTS website          VARCHAR(255),
    ADD COLUMN IF NOT EXISTS notes            TEXT,
    ADD COLUMN IF NOT EXISTS average_rating   NUMERIC(3,2),
    ADD COLUMN IF NOT EXISTS rating_count     INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS last_rated_at    TIMESTAMP;

CREATE INDEX IF NOT EXISTS ix_suppliers_group_name ON suppliers(group_name);

-- 2) Tạo bảng supplier_ratings
CREATE TABLE IF NOT EXISTS supplier_ratings (
    rating_id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    supplier_id        UUID NOT NULL,
    purchase_order_id  UUID,
    quality_score      SMALLINT NOT NULL CHECK (quality_score  BETWEEN 1 AND 5),
    delivery_score     SMALLINT NOT NULL CHECK (delivery_score BETWEEN 1 AND 5),
    price_score        SMALLINT NOT NULL CHECK (price_score    BETWEEN 1 AND 5),
    service_score      SMALLINT NOT NULL CHECK (service_score  BETWEEN 1 AND 5),
    overall_rating     NUMERIC(3,2) NOT NULL,
    comment            TEXT,
    rated_by           UUID,
    rated_at           TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT supplier_ratings_supplier_id_fkey
        FOREIGN KEY (supplier_id) REFERENCES suppliers(supplier_id) ON DELETE CASCADE,
    CONSTRAINT supplier_ratings_po_id_fkey
        FOREIGN KEY (purchase_order_id) REFERENCES purchase_orders(purchase_order_id) ON DELETE SET NULL,
    CONSTRAINT supplier_ratings_rated_by_fkey
        FOREIGN KEY (rated_by) REFERENCES users(user_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_supplier_ratings_supplier_id ON supplier_ratings(supplier_id);
CREATE INDEX IF NOT EXISTS ix_supplier_ratings_rated_at   ON supplier_ratings(rated_at DESC);

COMMIT;

-- ============================================================
-- ROLLBACK SCRIPT (chỉ chạy nếu cần revert)
-- ============================================================
-- BEGIN;
-- DROP TABLE IF EXISTS supplier_ratings;
-- ALTER TABLE suppliers
--     DROP COLUMN IF EXISTS contact_person,
--     DROP COLUMN IF EXISTS tax_code,
--     DROP COLUMN IF EXISTS group_name,
--     DROP COLUMN IF EXISTS bank_account,
--     DROP COLUMN IF EXISTS bank_name,
--     DROP COLUMN IF EXISTS website,
--     DROP COLUMN IF EXISTS notes,
--     DROP COLUMN IF EXISTS average_rating,
--     DROP COLUMN IF EXISTS rating_count,
--     DROP COLUMN IF EXISTS last_rated_at;
-- COMMIT;
