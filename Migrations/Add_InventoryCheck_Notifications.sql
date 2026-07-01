-- =====================================================
-- MIGRATION: Add InventoryCheck Recount Fields & Notifications
-- Database: PostgreSQL (M.E.P Database)
-- Date: 2026-07-01
-- =====================================================

-- =====================================================
-- PART 1: Thêm bảng Notifications
-- =====================================================
CREATE TABLE IF NOT EXISTS notifications (
    notification_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    type INTEGER NOT NULL DEFAULT 1,
    recipient_user_id UUID NULL,
    branch_id UUID NOT NULL,
    inventory_check_id UUID NULL,
    triggered_by_user_id UUID NOT NULL,
    message TEXT NOT NULL,
    is_read BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Indexes cho Notifications
CREATE INDEX IF NOT EXISTS idx_notifications_recipient ON notifications(recipient_user_id);
CREATE INDEX IF NOT EXISTS idx_notifications_branch ON notifications(branch_id);
CREATE INDEX IF NOT EXISTS idx_notifications_inventory_check ON notifications(inventory_check_id);
CREATE INDEX IF NOT EXISTS idx_notifications_created_at ON notifications(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_notifications_unread ON notifications(recipient_user_id, is_read) WHERE is_read = FALSE;

-- =====================================================
-- PART 2: Thêm cột vào bảng InventoryCheckTickets
-- =====================================================
ALTER TABLE inventory_check_tickets
    ADD COLUMN IF NOT EXISTS recount_number INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS recount_requested_at TIMESTAMP WITHOUT TIME ZONE NULL,
    ADD COLUMN IF NOT EXISTS recount_requested_by_user_id UUID NULL,
    ADD COLUMN IF NOT EXISTS recount_reason TEXT NULL;

-- =====================================================
-- PART 3: Cập nhật mapping trong DbContext (code only)
-- NOTE: Cần thêm vào OnModelCreating của MEPDatabaseContext:
-- =====================================================
-- THÊM VÀO modelBuilder.Entity<Notification>(entity => { ... })
-- Sau dòng entity.HasOne(e => e.LinkedStockTicket)... và trước dấu }); của InventoryCheckTicket:

/*
// Recount fields
entity.Property(e => e.RecountNumber)
    .HasColumnName("recount_number")
    .HasDefaultValue(0);
entity.Property(e => e.RecountRequestedAt)
    .HasColumnType("timestamp without time zone")
    .HasColumnName("recount_requested_at");
entity.Property(e => e.RecountRequestedByUserId)
    .HasColumnName("recount_requested_by_user_id");
entity.Property(e => e.RecountReason)
    .HasMaxLength(500)
    .HasColumnName("recount_reason");

// Notifications
entity.HasMany(e => e.Notifications)
    .WithOne(n => n.InventoryCheck)
    .HasForeignKey(n => n.InventoryCheckId)
    .OnDelete(DeleteBehavior.SetNull);
*/

-- =====================================================
-- THÊM MỚI modelBuilder.Entity<Notification>(entity => { ... })
-- Đặt sau modelBuilder.Entity<InventoryCheckTicket>... và trước modelBuilder.Entity<InventoryTransferTicket>
-- =====================================================

/*
modelBuilder.Entity<Notification>(entity =>
{
    entity.HasKey(e => e.NotificationId).HasName("notifications_pkey");
    entity.ToTable("notifications");

    entity.Property(e => e.NotificationId)
        .HasDefaultValueSql("uuid_generate_v4()")
        .HasColumnName("notification_id");
    entity.Property(e => e.Type)
        .HasColumnName("type");
    entity.Property(e => e.RecipientUserId)
        .HasColumnName("recipient_user_id");
    entity.Property(e => e.BranchId)
        .HasColumnName("branch_id");
    entity.Property(e => e.InventoryCheckId)
        .HasColumnName("inventory_check_id");
    entity.Property(e => e.TriggeredByUserId)
        .HasColumnName("triggered_by_user_id");
    entity.Property(e => e.Message)
        .HasColumnName("message");
    entity.Property(e => e.IsRead)
        .HasDefaultValue(false)
        .HasColumnName("is_read");
    entity.Property(e => e.CreatedAt)
        .HasColumnType("timestamp without time zone")
        .HasColumnName("created_at");

    // Foreign keys
    entity.HasOne(e => e.RecipientUser)
        .WithMany()
        .HasForeignKey(e => e.RecipientUserId)
        .OnDelete(DeleteBehavior.SetNull);

    entity.HasOne(e => e.TriggeredByUser)
        .WithMany()
        .HasForeignKey(e => e.TriggeredByUserId)
        .OnDelete(DeleteBehavior.Restrict);

    entity.HasOne(e => e.Branch)
        .WithMany()
        .HasForeignKey(e => e.BranchId)
        .OnDelete(DeleteBehavior.Restrict);
});
*/

-- =====================================================
-- PART 4: Seed Data cho NotificationType enum
-- =====================================================
-- NotificationType:
-- 1 = Assigned
-- 2 = Submitted
-- 3 = Approved
-- 4 = RecountRequested
-- 5 = Created

-- =====================================================
-- VERIFICATION: Kiểm tra các bảng đã được tạo đúng
-- =====================================================
DO $$
BEGIN
    -- Check notifications table
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public'
        AND table_name = 'notifications'
    ) THEN
        RAISE NOTICE 'Table notifications created successfully';
    ELSE
        RAISE WARNING 'Table notifications was NOT created!';
    END IF;

    -- Check inventory_check_tickets columns
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'inventory_check_tickets'
        AND column_name = 'recount_number'
    ) THEN
        RAISE NOTICE 'Column recount_number added successfully';
    ELSE
        RAISE WARNING 'Column recount_number was NOT added!';
    END IF;
END $$;

-- =====================================================
-- ROLLBACK SCRIPT (nếu cần)
-- =====================================================
-- DROP TABLE IF EXISTS notifications;
-- ALTER TABLE inventory_check_tickets
--     DROP COLUMN IF EXISTS recount_number,
--     DROP COLUMN IF EXISTS recount_requested_at,
--     DROP COLUMN IF EXISTS recount_requested_by_user_id,
--     DROP COLUMN IF EXISTS recount_reason;
