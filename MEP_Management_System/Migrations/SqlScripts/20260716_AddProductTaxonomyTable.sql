-- Tạo bảng product_taxonomies để lưu nhóm hàng và thương hiệu
-- Type: 'Category' | 'Brand'

CREATE TABLE IF NOT EXISTS product_taxonomies (
    taxonomy_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    type VARCHAR(50) NOT NULL CHECK (type IN ('Category', 'Brand')),
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(name, type)
);

-- Index để tìm nhanh theo loại
CREATE INDEX IF NOT EXISTS idx_product_taxonomies_type ON product_taxonomies(type);
