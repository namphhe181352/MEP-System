using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Models;

public partial class MEPDatabaseContext : DbContext
{
    public MEPDatabaseContext()
    {
    }

    public MEPDatabaseContext(DbContextOptions<MEPDatabaseContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<BranchProduct> BranchProducts { get; set; }

    public virtual DbSet<GoodsIssue> GoodsIssues { get; set; }

    public virtual DbSet<GoodsIssueItem> GoodsIssueItems { get; set; }

    public virtual DbSet<GoodsIssueAttachment> GoodsIssueAttachments { get; set; }

    public virtual DbSet<InventoryCheckTicket> InventoryCheckTickets { get; set; }
    public virtual DbSet<InventoryCheckDetail> InventoryCheckDetails { get; set; }
    public virtual DbSet<InventoryTransferTicket> InventoryTransferTickets { get; set; }
    public virtual DbSet<InventoryTransferDetail> InventoryTransferDetails { get; set; }
public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoiceItem> InvoiceItems { get; set; }
    public virtual DbSet<InventoryLedger> InventoryLedgers { get; set; }
    public virtual DbSet<DailyStockSnapshot> DailyStockSnapshots { get; set; }

    public virtual DbSet<OrderDiscountTier> OrderDiscountTiers { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<MediaResource> MediaResources { get; set; }

    public virtual DbSet<OtpVerification> OtpVerifications { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductAttribute> ProductAttributes { get; set; }

    public virtual DbSet<ProductConversionUnit> ProductConversionUnits { get; set; }

    public virtual DbSet<ProductTaxonomy> ProductTaxonomies { get; set; }
    public virtual DbSet<ProductAttributeType> ProductAttributeTypes { get; set; }
    public virtual DbSet<StoreApproval> StoreApprovals { get; set; }

    public virtual DbSet<ProductLocation> ProductLocations { get; set; }

    public virtual DbSet<PurchaseOrder> PurchaseOrders { get; set; }

    public virtual DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }

    public virtual DbSet<ReturnItem> ReturnItems { get; set; }

    public virtual DbSet<ReturnOrder> ReturnOrders { get; set; }

    public virtual DbSet<ExchangeItem> ExchangeItems { get; set; }

    public virtual DbSet<WarrantyTicket> WarrantyTickets { get; set; }
    public virtual DbSet<WarrantyClaim> WarrantyClaims { get; set; }

    public virtual DbSet<WarrantyLotAllocation> WarrantyLotAllocations { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<StaffAssignment> StaffAssignments { get; set; }

    public virtual DbSet<StockTicket> StockTickets { get; set; }

    public virtual DbSet<StockTicketItem> StockTicketItems { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }
public virtual DbSet<SystemConfig> SystemConfigs { get; set; }
public virtual DbSet<SystemLog> SystemLogs { get; set; }

    public virtual DbSet<SystemNotification> SystemNotifications { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserPermission> UserPermissions { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }
public virtual DbSet<Shift> Shifts { get; set; }
public virtual DbSet<RefundRecord> RefundRecords { get; set; }

    public virtual DbSet<Receipt> Receipts { get; set; }
public virtual DbSet<SupplierPayment> SupplierPayments { get; set; }

    // === EXPENSE MODULE ===
    public virtual DbSet<ExpenseCategory> ExpenseCategories { get; set; }
    public virtual DbSet<ExpenseVoucher> ExpenseVouchers { get; set; }
    public virtual DbSet<StockPrintTemplate> StockPrintTemplates { get; set; }
    public virtual DbSet<StockTicketReceipt> StockTicketReceipts { get; set; }
    public virtual DbSet<InvoicePrintTemplate> InvoicePrintTemplates { get; set; }
    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Sửa tên Database thành "mep_db" (viết liền, chữ thường, chuẩn Postgres)
            optionsBuilder.UseNpgsql("Host=103.75.182.211;Port=5432;Database=M.E.P Database;Username=postgres;Password=Vuatrom154321@");
        }
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.BranchId).HasName("branches_pkey");

            entity.ToTable("branches");

            entity.Property(e => e.BranchId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("branch_id");
            entity.Property(e => e.BranchCode)
                .HasMaxLength(50)
                .HasColumnName("branch_code");
            entity.Property(e => e.Phone)
                .HasMaxLength(50)
                .HasColumnName("phone");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasColumnName("city");
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .HasColumnName("address");
            entity.Property(e => e.BranchName)
                .HasMaxLength(100)
                .HasColumnName("branch_name");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");
            entity.Property(e => e.ManagerUserId)
                .HasColumnName("manager_user_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue((short)1)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.ManagerUser)
                .WithMany()
                .HasForeignKey(d => d.ManagerUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<SupplierPayment>(entity =>
        {
            entity.HasKey(e => e.PaymentId);
            entity.ToTable("SupplierPayments");
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaymentMethod).HasMaxLength(50);
            entity.Property(e => e.ReferenceCode).HasMaxLength(100);
            entity.Property(e => e.IsDeleted).HasDefaultValue((short)0);

            entity.HasOne(d => d.Supplier)
                .WithMany(s => s.SupplierPayments)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.PurchaseOrder)
                .WithMany()
                .HasForeignKey(d => d.PurchaseOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(e => e.CreatedBy).HasColumnName("created_by");

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("supplier_payments_created_by_fkey");
        });
        modelBuilder.Entity<BranchProduct>(entity =>
        {
            entity.HasKey(e => e.BranchProductId).HasName("branch_products_pkey");

            entity.ToTable("branch_products");

            entity.HasIndex(e => new { e.BranchId, e.ProductId }, "uk_branch_product").IsUnique();

            entity.Property(e => e.BranchProductId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("branch_product_id");
            entity.Property(e => e.ActualStock)
                .HasPrecision(10, 2)
                .HasDefaultValue(0m)
                .HasColumnName("actual_stock");
            entity.Property(e => e.AvailableStock)
                .HasPrecision(10, 2)
                .HasDefaultValue(0m)
                .HasColumnName("available_stock");
            entity.Property(e => e.WarrantyQuantity)
                .HasPrecision(10, 2)
                .HasDefaultValue(0m)
                .HasColumnName("warranty_quantity");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CostPrice)
                .HasPrecision(15, 2)
                .HasColumnName("cost_price");
            entity.Property(e => e.DirectSale)
                .HasDefaultValue((short)1)
                .HasColumnName("direct_sale");
            entity.Property(e => e.EstimatedOutAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("estimated_out_at");
            entity.Property(e => e.IsActive)
                .HasDefaultValue((short)0)
                .HasColumnName("is_active");
            entity.Property(e => e.MaximumStock)
                .HasPrecision(10, 2)
                .HasDefaultValue(100m)
                .HasColumnName("maximum_stock");
            entity.Property(e => e.MinimumStock)
                .HasPrecision(10, 2)
                .HasDefaultValue(10m)
                .HasColumnName("minimum_stock");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.ReservedStock)
                .HasPrecision(10, 2)
                .HasDefaultValue(0m)
                .HasColumnName("reserved_stock");
            entity.Property(e => e.SellPrice)
                .HasPrecision(15, 2)
                .HasColumnName("sell_price");
            entity.Property(e => e.ShelfLocation)
                .HasMaxLength(100)
                .HasColumnName("shelf_location");

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchProducts)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_products_branch_id_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.BranchProducts)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_products_product_id_fkey");
        });

        modelBuilder.Entity<InventoryCheckTicket>(entity =>
        {
            entity.HasKey(e => e.TicketId).HasName("inventory_check_tickets_pkey");
            entity.ToTable("inventory_check_tickets");

            entity.Property(e => e.TicketId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("ticket_id");
            entity.Property(e => e.TicketCode)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("ticket_code");
            // Ensure ticket code is unique
            entity.HasIndex(e => e.TicketCode).IsUnique().HasDatabaseName("uk_inventory_check_ticket_code");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.BranchId)
                .HasColumnName("branch_id");
            entity.Property(e => e.CreatedByUserId)
                .HasColumnName("created_by_user_id");
            entity.Property(e => e.AssigneeUserId)
                .HasColumnName("assignee_user_id");
            entity.Property(e => e.FilledByUserId)
                .HasColumnName("filled_by_user_id");
            entity.Property(e => e.FilledAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("filled_at");
            entity.Property(e => e.ApprovedByUserId)
                .HasColumnName("approved_by_user_id");
            entity.Property(e => e.ApprovedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("approved_at");
            entity.Property(e => e.CancelledByUserId)
                .HasColumnName("cancelled_by_user_id");
            entity.Property(e => e.CancelledAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("cancelled_at");
            entity.Property(e => e.CancelledReason)
                .HasMaxLength(1000)
                .HasColumnName("cancelled_reason");
            entity.Property(e => e.LinkedStockTicketId)
                .HasColumnName("linked_stock_ticket_id");
            entity.Property(e => e.RecountNumber)
                .HasDefaultValue(0)
                .HasColumnName("recount_number");
            entity.Property(e => e.RecountRequestedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("recount_requested_at");
            entity.Property(e => e.RecountRequestedByUserId)
                .HasColumnName("recount_requested_by_user_id");
            entity.Property(e => e.RecountReason)
                .HasMaxLength(1000)
                .HasColumnName("recount_reason");
            entity.Property(e => e.Notes)
                .HasMaxLength(1000)
                .HasColumnName("notes");

            entity.HasMany(e => e.Details)
                .WithOne(d => d.Ticket)
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            // Navigation properties for audit fields
            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("inventory_check_tickets_created_by_user_id_fkey");

            entity.HasOne(d => d.AssigneeUser)
                .WithMany()
                .HasForeignKey(d => d.AssigneeUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("inventory_check_tickets_assignee_user_id_fkey");

            entity.HasOne(d => d.FilledByUser)
                .WithMany()
                .HasForeignKey(d => d.FilledByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("inventory_check_tickets_filled_by_user_id_fkey");

            entity.HasOne(d => d.ApprovedByUser)
                .WithMany()
                .HasForeignKey(d => d.ApprovedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("inventory_check_tickets_approved_by_user_id_fkey");

            entity.HasOne(d => d.CancelledByUser)
                .WithMany()
                .HasForeignKey(d => d.CancelledByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("inventory_check_tickets_cancelled_by_user_id_fkey");

            entity.HasOne(d => d.LinkedStockTicket)
                .WithMany()
                .HasForeignKey(d => d.LinkedStockTicketId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("inventory_check_tickets_linked_stock_ticket_id_fkey");

            entity.HasOne(d => d.RecountRequestedByUser)
                .WithMany()
                .HasForeignKey(d => d.RecountRequestedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("inventory_check_tickets_recount_requested_by_user_id_fkey");

            entity.HasOne(d => d.Branch)
                .WithMany()
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("inventory_check_tickets_branch_id_fkey");
        });

        modelBuilder.Entity<InventoryTransferTicket>(entity =>
        {
            entity.HasKey(e => e.TransferId).HasName("inventory_transfer_tickets_pkey");
            entity.ToTable("inventory_transfer_tickets");

            entity.Property(e => e.TransferId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("transfer_id");
            entity.Property(e => e.TransferCode)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("transfer_code");
            // unique code
            entity.HasIndex(e => e.TransferCode).IsUnique().HasDatabaseName("uk_inventory_transfer_code");
            entity.Property(e => e.FromBranchId).HasColumnName("from_branch_id");
            entity.Property(e => e.ToBranchId).HasColumnName("to_branch_id");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50).HasColumnName("status");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.Notes).HasMaxLength(1000).HasColumnName("notes");

            entity.HasMany(e => e.Details).WithOne(d => d.Transfer).HasForeignKey(d => d.TransferId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventoryTransferDetail>(entity =>
        {
            entity.HasKey(e => e.DetailId).HasName("inventory_transfer_details_pkey");
            entity.ToTable("inventory_transfer_details");

            entity.Property(e => e.DetailId).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("detail_id");
            entity.Property(e => e.TransferId).HasColumnName("transfer_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.TransferQuantity).HasColumnName("transfer_quantity");

            entity.HasOne(d => d.Product).WithMany().HasForeignKey(d => d.ProductId).OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<InventoryCheckDetail>(entity =>
        {
            entity.HasKey(e => e.DetailId).HasName("inventory_check_details_pkey");
            entity.ToTable("inventory_check_details");

            entity.Property(e => e.DetailId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("detail_id");
            entity.Property(e => e.TicketId)
                .HasColumnName("ticket_id");
            entity.Property(e => e.ProductId)
                .HasColumnName("product_id");
            entity.Property(e => e.SystemQuantity)
                .HasColumnName("system_quantity");
            entity.Property(e => e.ActualQuantity)
                .HasColumnName("actual_quantity");
            entity.Property(e => e.Discrepancy)
                .HasColumnName("discrepancy");
            entity.Property(e => e.IsCounted)
                .HasColumnName("is_counted");
            entity.Property(e => e.ReasonNote)
                .HasMaxLength(1000)
                .HasColumnName("reason_note");
            entity.Property(e => e.CountedByUserId)
                .HasColumnName("counted_by_user_id");
            entity.Property(e => e.CountedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("counted_at");

            entity.HasOne(d => d.Product)
                .WithMany()
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.CountedByUser)
                .WithMany()
                .HasForeignKey(d => d.CountedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("inventory_check_details_counted_by_user_id_fkey");
        });




        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("customers_pkey");

            entity.ToTable("customers");

            entity.Property(e => e.CustomerId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("customer_id");
            entity.Property(e => e.CustomerName)
                .HasMaxLength(100)
                .HasColumnName("customer_name");
            entity.Property(e => e.LoyaltyPoints)
                .HasDefaultValue(0)
                .HasColumnName("loyalty_points");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .HasColumnName("phone_number");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .HasColumnName("email");
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .HasColumnName("address");
            entity.Property(e => e.DebtLimit)
                .HasPrecision(15, 2)
                .HasDefaultValue(0m)
                .HasColumnName("debt_limit");
            entity.Property(e => e.CurrentDebt)
                .HasPrecision(15, 2)
                .HasDefaultValue(0m)
                .HasColumnName("current_debt");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)1)
                .HasColumnName("status");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasIndex(e => e.PhoneNumber, "customers_phone_unique").IsUnique();

            entity.Property(e => e.Group)
                .HasMaxLength(50)
                .HasColumnName("group");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.TotalSpent)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total_spent");
            entity.Property(e => e.OrderCount)
                .HasDefaultValue(0)
                .HasColumnName("order_count");
            entity.Property(e => e.LastVisit)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_visit");
            entity.Property(e => e.MembershipTier)
                .HasMaxLength(50)
                .HasColumnName("membership_tier");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
            entity.Property(e => e.AdvanceBalance)
                .HasPrecision(15, 2)
                .HasDefaultValue(0m)
                .HasColumnName("advance_balance");
            entity.HasIndex(e => e.PhoneNumber, "customers_phone_unique").IsUnique();
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("invoices_pkey");

            entity.ToTable("invoices");

            entity.Property(e => e.InvoiceId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("invoice_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(50)
                .HasColumnName("payment_method");
            entity.Property(e => e.TotalAmount)
                .HasPrecision(15, 2)
                .HasColumnName("total_amount");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ShiftId).HasColumnName("shift_id");
            entity.Property(e => e.InvoiceCode)
                .HasMaxLength(50)
                .HasColumnName("invoice_code");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.HoldNote).HasMaxLength(500).HasColumnName("hold_note");
            entity.Property(e => e.HeldAt).HasColumnType("timestamp without time zone").HasColumnName("held_at");

            // Các cột chiết khấu theo tổng giá trị đơn hàng
            entity.Property(e => e.SubTotalAmount)
                .HasPrecision(18, 2)
                .HasColumnName("sub_total_amount")
                .HasDefaultValue(0m);
            entity.Property(e => e.DiscountPercent)
                .HasPrecision(5, 2)
                .HasColumnName("discount_percent");
            entity.Property(e => e.DiscountAmount)
                .HasPrecision(18, 2)
                .HasColumnName("discount_amount")
                .HasDefaultValue(0m);

            entity.HasIndex(e => e.InvoiceCode, "invoices_invoice_code_key").IsUnique();

            entity.HasOne(d => d.Order).WithOne(p => p.Invoice)
                .HasForeignKey<Invoice>(d => d.OrderId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("invoices_order_id_fkey");

            // Ensure 1-1 relationship: OrderId is unique in invoices table
            entity.HasIndex(e => e.OrderId).IsUnique();

            entity.HasOne(d => d.User).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("invoices_user_id_fkey");

            entity.HasOne(d => d.Shift).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("invoices_shift_id_fkey");
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(e => e.InvoiceItemId).HasName("invoice_items_pkey");

            entity.ToTable("invoice_items");

            entity.Property(e => e.InvoiceItemId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("invoice_item_id");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.BranchProductId).HasColumnName("branch_product_id");
            entity.Property(e => e.Quantity)
                .HasPrecision(10, 2)
                .HasColumnName("quantity");
            entity.Property(e => e.SellPrice)
                .HasPrecision(15, 2)
                .HasColumnName("sell_price");
            entity.Property(e => e.CostPrice)
                .HasPrecision(18, 4)
                .HasColumnName("cost_price");
            entity.Property(e => e.ReturnedQuantity)
                .HasPrecision(18, 4)
                .HasDefaultValue(0m)
                .HasColumnName("returned_quantity");

            entity.HasOne(d => d.BranchProduct).WithMany()
                .HasForeignKey(d => d.BranchProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("invoice_items_branch_product_id_fkey");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceItems)
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("invoice_items_invoice_id_fkey");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("orders_pkey");

            entity.ToTable("orders");

            entity.HasIndex(e => e.OrderCode, "orders_order_code_key").IsUnique();

            entity.Property(e => e.OrderId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("order_id");
            entity.Property(e => e.OrderCode)
                .HasMaxLength(50)
                .HasColumnName("order_code");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TotalAmount)
                .HasPrecision(15, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total_amount");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.DiscountAmount)
                .HasPrecision(15, 2)
                .HasColumnName("discount_amount");
            entity.Property(e => e.TaxAmount)
                .HasPrecision(15, 2)
                .HasColumnName("tax_amount");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(50)
                .HasColumnName("payment_method");
            entity.Property(e => e.Note).HasColumnName("note");

            entity.HasOne(d => d.Branch).WithMany(p => p.Orders)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orders_branch_id_fkey");

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("orders_customer_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Orders)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orders_user_id_fkey");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.OrderItemId).HasName("order_items_pkey");

            entity.ToTable("order_items");

            entity.Property(e => e.OrderItemId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("order_item_id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.BranchProductId).HasColumnName("branch_product_id");
            entity.Property(e => e.Quantity)
                .HasPrecision(10, 2)
                .HasColumnName("quantity");
            entity.Property(e => e.SellPrice)
                .HasPrecision(15, 2)
                .HasColumnName("sell_price");

            entity.HasOne(d => d.BranchProduct).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.BranchProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("order_items_branch_product_id_fkey");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("order_items_order_id_fkey");
        });

        modelBuilder.Entity<MediaResource>(entity =>
        {
            entity.HasKey(e => e.MediaId).HasName("media_resources_pkey");

            entity.ToTable("media_resources");

            entity.Property(e => e.MediaId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("media_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.IsPrimary)
                .HasDefaultValue((short)0)
                .HasColumnName("is_primary");
            entity.Property(e => e.MediaType)
                .HasMaxLength(20)
                .HasColumnName("media_type");
            entity.Property(e => e.MediaUrl)
                .HasColumnType("text")
                .HasColumnName("media_url");
            entity.Property(e => e.PostId).HasColumnName("post_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");


            entity.HasOne(d => d.Product).WithMany(p => p.MediaResources)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("media_resources_product_id_fkey");
        });

        modelBuilder.Entity<OtpVerification>(entity =>
        {
            entity.HasKey(e => e.OtpId).HasName("otp_verifications_pkey");

            entity.ToTable("otp_verifications");

            entity.Property(e => e.OtpId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("otp_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .HasColumnName("email");
            entity.Property(e => e.ExpiredAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("expired_at");
            entity.Property(e => e.IsUsed)
                .HasDefaultValue((short)0)
                .HasColumnName("is_used");
            entity.Property(e => e.OtpCode)
                .HasMaxLength(6)
                .HasColumnName("otp_code");
            entity.Property(e => e.Purpose)
                .HasMaxLength(50)
                .HasColumnName("purpose");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.PermissionId).HasName("permissions_pkey");

            entity.ToTable("permissions");

            entity.HasIndex(e => e.PermissionCode, "permissions_permission_code_key").IsUnique();

            entity.Property(e => e.PermissionId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("permission_id");
            entity.Property(e => e.PermissionCode)
                .HasMaxLength(100)
                .HasColumnName("permission_code");
            entity.Property(e => e.PermissionName)
                .HasMaxLength(100)
                .HasColumnName("permission_name");

            entity.HasMany(d => d.UserPermissions)
                .WithOne(up => up.Permission)
                .HasForeignKey(up => up.PermissionId)
                .HasConstraintName("user_permissions_permission_id_fkey");
        });

        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.HasKey(e => e.UserPermissionId).HasName("user_permissions_pkey");

            entity.ToTable("user_permissions");

            entity.Property(e => e.UserPermissionId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("user_permission_id");
            entity.Property(e => e.UserId)
                .HasColumnName("user_id");
            entity.Property(e => e.PermissionId)
                .HasColumnName("permission_id");
            entity.Property(e => e.IsGranted)
                .HasColumnName("is_granted");

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserPermissions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user_permissions_user_id_fkey");

            entity.HasOne(d => d.Permission)
                .WithMany(p => p.UserPermissions)
                .HasForeignKey(d => d.PermissionId)
                .HasConstraintName("user_permissions_permission_id_fkey");
        });

        // ------------------------------------------------------------------
        // ĐÃ SỬA: entity Product trước đây có 2 khối HasOne(CreatedByUser)/
        // HasOne(UpdatedByUser) trùng lặp (một khối ngay sau phần Property,
        // một khối lặp lại sau phần HasOne(Supplier) ở cuối). Chỉ giữ lại
        // MỘT khối duy nhất để tránh EF tính sai diff khi scaffold migration
        // (đây là nguyên nhân gốc của lỗi "column already exists" khi chạy
        // dotnet ef database update).
        // ------------------------------------------------------------------
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("products_pkey");

            entity.ToTable("products");

            entity.HasIndex(e => e.ProductCode, "products_product_code_key").IsUnique();

            entity.Property(e => e.ProductId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("product_id");
            entity.Property(e => e.Barcode)
                .HasMaxLength(50)
                .HasColumnName("barcode");
            entity.Property(e => e.BaseUnit)
                .HasMaxLength(20)
                .HasColumnName("base_unit");
            entity.Property(e => e.BrandName)
                .HasMaxLength(100)
                .HasColumnName("brand_name");
            entity.Property(e => e.CategoryName)
                .HasMaxLength(100)
                .HasColumnName("category_name");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("products_created_by_fkey")
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(d => d.UpdatedByUser)
                .WithMany()
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("products_updated_by_fkey")
                .OnDelete(DeleteBehavior.SetNull);
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
            entity.Property(e => e.Height)
                .HasPrecision(10, 2)
                .HasColumnName("height");
            entity.Property(e => e.ItemType)
                .HasMaxLength(50)
                .HasColumnName("item_type");
            entity.Property(e => e.Length)
                .HasPrecision(10, 2)
                .HasColumnName("length");
            entity.Property(e => e.ProductCode)
                .HasMaxLength(50)
                .HasColumnName("product_code");
            entity.Property(e => e.ProductName)
                .HasMaxLength(255)
                .HasColumnName("product_name");
            entity.Property(e => e.SalesChannelLinked)
                .HasDefaultValue((short)1)
                .HasColumnName("sales_channel_linked");
            entity.Property(e => e.WarrantyPeriod)
                .HasDefaultValue(12)
                .HasColumnName("warranty_period");
            entity.Property(e => e.WarrantyUnit)
                .HasMaxLength(10)
                .HasDefaultValue("MONTH")
                .HasColumnName("warranty_unit");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)1)
                .HasColumnName("status");
            entity.Property(e => e.SizeUnit)
                .HasMaxLength(10)
                .HasColumnName("size_unit");
            entity.Property(e => e.Specification)
                .HasMaxLength(100)
                .HasColumnName("specification");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.Weight)
                .HasPrecision(10, 2)
                .HasColumnName("weight");
            entity.Property(e => e.WeightUnit)
                .HasMaxLength(10)
                .HasColumnName("weight_unit");
            entity.Property(e => e.Width)
                .HasPrecision(10, 2)
                .HasColumnName("width");

            entity.HasOne(d => d.Supplier).WithMany(p => p.Products)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("products_supplier_id_fkey");

            entity.HasOne(d => d.CreatedByUser).WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("products_created_by_fkey")
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.UpdatedByUser).WithMany()
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("products_updated_by_fkey")
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductAttribute>(entity =>
        {
            entity.HasKey(e => e.AttributeId).HasName("product_attributes_pkey");

            entity.ToTable("product_attributes");

            entity.Property(e => e.AttributeId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("attribute_id");
            entity.Property(e => e.AttributeName)
                .HasMaxLength(100)
                .HasColumnName("attribute_name");
            entity.Property(e => e.AttributeValue)
                .HasMaxLength(255)
                .HasColumnName("attribute_value");
            entity.Property(e => e.ProductId).HasColumnName("product_id");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductAttributes)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("product_attributes_product_id_fkey");
        });

        modelBuilder.Entity<ProductConversionUnit>(entity =>
        {
            entity.HasKey(e => e.ConversionId).HasName("product_conversion_units_pkey");

            entity.ToTable("product_conversion_units");

            entity.Property(e => e.ConversionId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("conversion_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.ConvertValue)
                .HasPrecision(10, 2)
                .HasColumnName("convert_value");
            entity.Property(e => e.DirectSale)
                .HasDefaultValue((short)1)
                .HasColumnName("direct_sale");
            entity.Property(e => e.Price)
                .HasPrecision(15, 2)
                .HasColumnName("price");
            entity.Property(e => e.UnitName)
                .HasMaxLength(20)
                .HasColumnName("unit_name");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductConversionUnits)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("product_conversion_units_product_id_fkey");
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.PurchaseOrderId).HasName("purchase_orders_pkey");

            entity.ToTable("purchase_orders");

            entity.Property(e => e.PurchaseOrderId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("purchase_order_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.TotalAmount)
                .HasPrecision(15, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("total_amount");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.OrderCode)
                .HasMaxLength(50)
                .HasColumnName("order_code");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.ApprovedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("approved_at");
            entity.Property(e => e.ExpectedDeliveryDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("expected_delivery_date");
            entity.Property(e => e.Notes).HasColumnName("notes");

            entity.HasIndex(e => e.OrderCode, "purchase_orders_order_code_key").IsUnique();

            entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("purchase_orders_supplier_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("purchase_orders_user_id_fkey");

            entity.HasOne(d => d.ApprovedByUser).WithMany()
                .HasForeignKey(d => d.ApprovedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("purchase_orders_approved_by_fkey");
        });

        modelBuilder.Entity<PurchaseOrderItem>(entity =>
        {
            entity.HasKey(e => e.PoItemId).HasName("purchase_order_items_pkey");

            entity.ToTable("purchase_order_items");

            entity.Property(e => e.PoItemId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("po_item_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.PurchaseOrderId).HasColumnName("purchase_order_id");
            entity.Property(e => e.Quantity)
                .HasPrecision(10, 2)
                .HasColumnName("quantity");
            entity.Property(e => e.UnitPrice)
                .HasPrecision(15, 2)
                .HasColumnName("unit_price");

            entity.HasOne(d => d.Product).WithMany(p => p.PurchaseOrderItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("purchase_order_items_product_id_fkey");

            entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.PurchaseOrderItems)
                .HasForeignKey(d => d.PurchaseOrderId)
                .HasConstraintName("purchase_order_items_purchase_order_id_fkey");
        });

        modelBuilder.Entity<ReturnItem>(entity =>
        {
            entity.HasKey(e => e.ReturnItemId).HasName("return_items_pkey");

            entity.ToTable("return_items");

            entity.Property(e => e.ReturnItemId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("return_item_id");
            entity.Property(e => e.BranchProductId).HasColumnName("branch_product_id");
            entity.Property(e => e.InvoiceItemId).HasColumnName("invoice_item_id");
            entity.Property(e => e.Quantity)
                .HasPrecision(10, 2)
                .HasColumnName("quantity");
            entity.Property(e => e.ConversionRate)
                .HasPrecision(10, 2)
                .HasDefaultValue(1m)
                .HasColumnName("conversion_rate");
            entity.Property(e => e.BaseQuantity)
                .HasPrecision(10, 2)
                .HasDefaultValue(0m)
                .HasColumnName("base_quantity");
            entity.Property(e => e.ReturnOrderId).HasColumnName("return_order_id");
            entity.Property(e => e.RefundAmount)
                .HasPrecision(15, 2)
                .HasColumnName("refund_amount");
            entity.Property(e => e.Reason)
                .HasMaxLength(100)
                .HasColumnName("reason");

            entity.HasOne(d => d.BranchProduct).WithMany(p => p.ReturnItems)
                .HasForeignKey(d => d.BranchProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("return_items_branch_product_id_fkey");

            entity.HasOne(d => d.ReturnOrder).WithMany(p => p.ReturnItems)
                .HasForeignKey(d => d.ReturnOrderId)
                .HasConstraintName("return_items_return_order_id_fkey");
        });

        modelBuilder.Entity<WarrantyTicket>(entity =>
        {
            entity.HasKey(e => e.WarrantyTicketId).HasName("warranty_tickets_pkey");

            entity.ToTable("warranty_tickets");

            entity.Property(e => e.WarrantyTicketId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("warranty_ticket_id");
            entity.Property(e => e.ReturnItemId).HasColumnName("return_item_id");
            entity.Property(e => e.BranchProductId).HasColumnName("branch_product_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity)
                .HasPrecision(10, 2)
                .HasColumnName("quantity");
            entity.Property(e => e.ConversionRate)
                .HasPrecision(10, 2)
                .HasDefaultValue(1m)
                .HasColumnName("conversion_rate");
            entity.Property(e => e.BaseQuantity)
                .HasPrecision(10, 2)
                .HasDefaultValue(0m)
                .HasColumnName("base_quantity");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.Notes)
                .HasMaxLength(500)
                .HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.AssignedAt).HasColumnName("assigned_at");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CompletedBy).HasColumnName("completed_by");

            entity.HasOne(d => d.ReturnItem).WithOne(p => p.WarrantyTicket)
                .HasForeignKey<WarrantyTicket>(d => d.ReturnItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("warranty_tickets_return_item_id_fkey");

            entity.HasOne(d => d.Supplier).WithMany()
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("warranty_tickets_supplier_id_fkey");
        });

        modelBuilder.Entity<WarrantyLotAllocation>(entity =>
        {
            entity.HasKey(e => e.AllocationId).HasName("warranty_lot_allocations_pkey");

            entity.ToTable("warranty_lot_allocations");

            entity.Property(e => e.AllocationId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("allocation_id");
            entity.Property(e => e.WarrantyTicketId).HasColumnName("warranty_ticket_id");
            entity.Property(e => e.TicketItemId).HasColumnName("ticket_item_id");
            entity.Property(e => e.AllocateQty)
                .HasPrecision(15, 2)
                .HasColumnName("allocate_qty");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");

            entity.HasOne(d => d.WarrantyTicket).WithMany()
                .HasForeignKey(d => d.WarrantyTicketId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("warranty_lot_allocations_warranty_ticket_id_fkey");

            entity.HasOne(d => d.StockTicketItem).WithMany()
                .HasForeignKey(d => d.TicketItemId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("warranty_lot_allocations_ticket_item_id_fkey");

            entity.HasIndex(d => d.WarrantyTicketId);
            entity.HasIndex(d => d.TicketItemId);
        });

        modelBuilder.Entity<ReturnOrder>(entity =>
        {
            entity.HasKey(e => e.ReturnOrderId).HasName("return_orders_pkey");

            entity.ToTable("return_orders");

            entity.Property(e => e.ReturnOrderId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("return_order_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.RefundAmount)
                .HasPrecision(15, 2)
                .HasColumnName("refund_amount");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ReturnCode)
                .HasMaxLength(50)
                .HasColumnName("return_code");
            entity.Property(e => e.RefundMethod)
                .HasMaxLength(50)
                .HasColumnName("refund_method");
            entity.Property(e => e.ReturnType)
                .HasMaxLength(50)
                .HasColumnName("return_type");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.Note).HasColumnName("note");

            entity.HasIndex(e => e.ReturnCode, "return_orders_return_code_key").IsUnique();

            entity.HasOne(d => d.Order).WithMany(p => p.ReturnOrders)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("return_orders_order_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.ReturnOrders)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("return_orders_user_id_fkey");
        });

        modelBuilder.Entity<ReturnOrder>(entity =>
        {
            entity.Property(e => e.DeltaAmount).HasPrecision(18, 2).HasColumnName("delta_amount");
            entity.Property(e => e.PayAmount).HasPrecision(18, 2).HasColumnName("pay_amount");
            entity.Property(e => e.RefundAmountCustomer).HasPrecision(18, 2).HasColumnName("refund_amount_customer");
            entity.Property(e => e.PaymentMethod).HasMaxLength(50).HasColumnName("payment_method");
            entity.Property(e => e.PaymentId).HasColumnName("payment_id");
        });

        modelBuilder.Entity<ExchangeItem>(entity =>
        {
            entity.HasKey(e => e.ExchangeItemId).HasName("exchange_items_pkey");

            entity.ToTable("exchange_items");

            entity.Property(e => e.ExchangeItemId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("exchange_item_id");
            entity.Property(e => e.ReturnOrderId).HasColumnName("return_order_id");
            entity.Property(e => e.BranchProductId).HasColumnName("branch_product_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasPrecision(18, 4).HasColumnName("quantity");
            entity.Property(e => e.ConversionRate).HasPrecision(18, 4).HasColumnName("conversion_rate");
            entity.Property(e => e.BaseQuantity).HasPrecision(18, 4).HasColumnName("base_quantity");
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2).HasColumnName("unit_price");
            entity.Property(e => e.LineTotal).HasPrecision(18, 2).HasColumnName("line_total");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasOne(d => d.ReturnOrder).WithMany(p => p.ExchangeItems)
                .HasForeignKey(d => d.ReturnOrderId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("exchange_items_return_order_id_fkey");

            entity.HasOne(d => d.BranchProduct).WithMany()
                .HasForeignKey(d => d.BranchProductId)
                .HasConstraintName("exchange_items_branch_product_id_fkey");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("roles_pkey");

            entity.ToTable("roles");

            entity.HasIndex(e => e.RoleName, "roles_role_name_key").IsUnique();

            entity.Property(e => e.RoleId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("role_id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.IsSystem)
                .HasDefaultValue((short)0)
                .HasColumnName("is_system");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .HasColumnName("role_name");

            entity.HasMany(d => d.Permissions).WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "RolePermission",
                    r => r.HasOne<Permission>().WithMany()
                        .HasForeignKey("PermissionId")
                        .HasConstraintName("role_permissions_permission_id_fkey"),
                    l => l.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .HasConstraintName("role_permissions_role_id_fkey"),
                    j =>
                    {
                        j.HasKey("RoleId", "PermissionId").HasName("role_permissions_pkey");
                        j.ToTable("role_permissions");
                        j.IndexerProperty<Guid>("RoleId").HasColumnName("role_id");
                        j.IndexerProperty<Guid>("PermissionId").HasColumnName("permission_id");
                    });
        });

        modelBuilder.Entity<StaffAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("staff_assignments_pkey");

            entity.ToTable("staff_assignments");

            entity.Property(e => e.AssignmentId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("assignment_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("assigned_at");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Branch).WithMany(p => p.StaffAssignments)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("staff_assignments_branch_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.StaffAssignments)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("staff_assignments_user_id_fkey");
        });

        modelBuilder.Entity<StockTicket>(entity =>
        {
            entity.HasKey(e => e.StockTicketId).HasName("stock_tickets_pkey");

            entity.ToTable("stock_tickets");

            entity.Property(e => e.StockTicketId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("stock_ticket_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.InwardTicketId).HasColumnName("inward_ticket_id");
            entity.Property(e => e.TotalAmount)
                .HasPrecision(15, 2)
                .HasColumnName("total_amount");
            entity.Property(e => e.TicketCode)
                .HasMaxLength(50)
                .HasColumnName("ticket_code");
            entity.Property(e => e.TicketType)
                .HasMaxLength(50)
                .HasColumnName("ticket_type");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.Notes).HasColumnName("notes");

            // === Audit trail ===
            entity.Property(e => e.CompletedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("completed_at");
            entity.Property(e => e.ConfirmedBy).HasColumnName("confirmed_by");
            entity.Property(e => e.CancelledAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("cancelled_at");
            entity.Property(e => e.CancelledBy).HasColumnName("cancelled_by");
            entity.Property(e => e.CancelReason).HasMaxLength(500).HasColumnName("cancel_reason");
            entity.Property(e => e.RecountNumber)
                .HasDefaultValue(0)
                .HasColumnName("recount_number");
            entity.Property(e => e.RecountRequestedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("recount_requested_at");
            entity.Property(e => e.RecountRequestedByUserId)
                .HasColumnName("recount_requested_by_user_id");
            entity.Property(e => e.RecountReason)
                .HasMaxLength(1000)
                .HasColumnName("recount_reason");

            entity.HasIndex(e => e.TicketCode, "stock_tickets_ticket_code_key").IsUnique();

            entity.HasOne(d => d.Branch).WithMany(p => p.StockTickets)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("stock_tickets_branch_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.StockTickets)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("stock_tickets_user_id_fkey");

            entity.HasOne(d => d.Supplier).WithMany(p => p.StockTickets)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("stock_tickets_supplier_id_fkey");

            entity.HasOne(d => d.ConfirmedByUser).WithMany()
                .HasForeignKey(d => d.ConfirmedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("stock_tickets_confirmed_by_fkey");

            entity.HasOne(d => d.CancelledByUser).WithMany()
                .HasForeignKey(d => d.CancelledBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("stock_tickets_cancelled_by_fkey");

            entity.HasOne(d => d.RecountRequestedByUser).WithMany()
                .HasForeignKey(d => d.RecountRequestedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("stock_tickets_recount_requested_by_user_id_fkey");

            // Self-referencing: phiếu xuất → phiếu nhập gốc
            entity.HasOne(d => d.InwardTicket).WithMany(p => p.OutwardTickets)
                .HasForeignKey(d => d.InwardTicketId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("stock_tickets_inward_ticket_id_fkey");
        });

        modelBuilder.Entity<StockTicketItem>(entity =>
        {
            entity.HasKey(e => e.TicketItemId).HasName("stock_ticket_items_pkey");

            entity.ToTable("stock_ticket_items");

            entity.Property(e => e.TicketItemId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("ticket_item_id");
            entity.Property(e => e.ActualQuantity)
                .HasPrecision(10, 2)
                .HasColumnName("actual_quantity");
            entity.Property(e => e.BranchProductId).HasColumnName("branch_product_id");
            entity.Property(e => e.Discrepancy)
                .HasPrecision(10, 2)
                .HasColumnName("discrepancy");
            entity.Property(e => e.StockTicketId).HasColumnName("stock_ticket_id");
            entity.Property(e => e.InwardTicketItemId).HasColumnName("inward_ticket_item_id");
            entity.Property(e => e.SystemQuantity)
                .HasPrecision(10, 2)
                .HasColumnName("system_quantity");
            entity.Property(e => e.UnitPrice)
                .HasPrecision(15, 2)
                .HasColumnName("unit_price");
            entity.Property(e => e.WarrantyPeriod)
                .HasDefaultValue(0)
                .HasColumnName("warranty_period");
            entity.Property(e => e.WarrantyUnit)
                .HasMaxLength(10)
                .HasDefaultValue("MONTH")
                .HasColumnName("warranty_unit");
            entity.Property(e => e.WarrantyExpiryDate)
                .HasColumnName("warranty_expiry_date");
            entity.Property(e => e.ClaimedWarrantyQty)
                .HasPrecision(15, 2)
                .HasDefaultValue(0m)
                .HasColumnName("claimed_warranty_qty");
            entity.Property(e => e.UnitName)
                .HasColumnName("unit_name");
            entity.Property(e => e.Notes)
                .HasColumnName("notes");


            entity.HasOne(d => d.BranchProduct).WithMany(p => p.StockTicketItems)
                .HasForeignKey(d => d.BranchProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("stock_ticket_items_branch_product_id_fkey");

            entity.HasOne(d => d.StockTicket).WithMany(p => p.StockTicketItems)
                .HasForeignKey(d => d.StockTicketId)
                .HasConstraintName("stock_ticket_items_stock_ticket_id_fkey");

            // Self-referencing: dòng xuất → dòng nhập gốc
            entity.HasOne(d => d.InwardTicketItem).WithMany(p => p.OutwardTicketItems)
                .HasForeignKey(d => d.InwardTicketItemId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("stock_ticket_items_inward_ticket_item_id_fkey");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("suppliers_pkey");

            entity.ToTable("suppliers");

            entity.Property(e => e.SupplierId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("supplier_id");
            entity.Property(e => e.ContactPhone)
                .HasMaxLength(20)
                .HasColumnName("contact_phone");
            entity.Property(e => e.SupplierName)
                .HasMaxLength(150)
                .HasColumnName("supplier_name");
            entity.Property(e => e.ContactEmail)
                .HasMaxLength(150)
                .HasColumnName("contact_email");
            entity.Property(e => e.ContactPerson)
                .HasMaxLength(100)
                .HasColumnName("contact_person");
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .HasColumnName("tax_code");
            entity.Property(e => e.GroupName)
                .HasMaxLength(50)
                .HasColumnName("group_name");
            entity.Property(e => e.BankAccount)
                .HasMaxLength(50)
                .HasColumnName("bank_account");
            entity.Property(e => e.BankName)
                .HasMaxLength(100)
                .HasColumnName("bank_name");
            entity.Property(e => e.Website)
                .HasMaxLength(255)
                .HasColumnName("website");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .HasColumnName("address");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)1)
                .HasColumnName("status");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.AverageRating)
                .HasPrecision(3, 2)
                .HasColumnName("average_rating");
            entity.Property(e => e.RatingCount)
                .HasDefaultValue(0)
                .HasColumnName("rating_count");
            entity.Property(e => e.LastRatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_rated_at");

            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("suppliers_created_by_fkey");

            entity.HasOne(d => d.UpdatedByUser)
                .WithMany()
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("suppliers_updated_by_fkey");
        });


        modelBuilder.Entity<SystemConfig>(entity =>
        {
            entity.HasKey(e => e.ConfigId).HasName("system_configs_pkey");

            entity.ToTable("system_configs");

            entity.Property(e => e.ConfigId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("config_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.ConfigKey)
                .HasMaxLength(100)
                .HasColumnName("config_key");
            entity.Property(e => e.ConfigValue).HasColumnName("config_value");

            entity.HasOne(d => d.Branch).WithMany(p => p.SystemConfigs)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("system_configs_branch_id_fkey");
        });

        modelBuilder.Entity<SystemLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("system_logs_pkey");

            entity.ToTable("system_logs");

            entity.Property(e => e.LogId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("log_id");
            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .HasColumnName("action");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.TableName)
                .HasMaxLength(50)
                .HasColumnName("table_name");
            entity.Property(e => e.Timestamp)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("timestamp");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.EntityType)
                .HasMaxLength(50)
                .HasColumnName("entity_type");
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(50)
                .HasColumnName("ip_address");
            entity.Property(e => e.UserAgent)
                .HasMaxLength(255)
                .HasColumnName("user_agent");
            entity.Property(e => e.Level)
                .HasMaxLength(20)
                .HasColumnName("level");
            entity.Property(e => e.Source)
                .HasMaxLength(100)
                .HasColumnName("source");

            entity.HasOne(d => d.User).WithMany(p => p.SystemLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("system_logs_user_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_unique").IsUnique();

            entity.HasIndex(e => e.PhoneNumber, "users_phone_unique").IsUnique();

            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();

            entity.Property(e => e.UserId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasColumnName("full_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue((short)1)
                .HasColumnName("is_active");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .HasColumnName("phone_number");
            entity.Property(e => e.IsVerified)
                .HasDefaultValue(false)
                .HasColumnName("is_verified");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasColumnName("status");
            entity.Property(e => e.SuspendedUntil)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("suspended_until");
            entity.Property(e => e.SubscriptionPlan)
                .HasMaxLength(20)
                .HasColumnName("subscription_plan");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");
            entity.Property(e => e.CreatedBy)
                .HasColumnName("created_by");

            // entity.Property(e => e.DefaultBranchId)
            //     .HasColumnName("default_branch_id");

            // Self-referencing foreign key: User created by another User (Owner)
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedBy)
                .HasConstraintName("users_created_by_fkey")
                .OnDelete(DeleteBehavior.SetNull);

            // Foreign key: User's default branch
            // entity.HasOne(e => e.DefaultBranch)
            //     .WithMany()
            //     .HasForeignKey(e => e.DefaultBranchId)
            //     .HasConstraintName("users_default_branch_id_fkey")
            //     .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserRole",
                    r => r.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .HasConstraintName("user_roles_role_id_fkey"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .HasConstraintName("user_roles_user_id_fkey"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId").HasName("user_roles_pkey");
                        j.ToTable("user_roles");
                        j.IndexerProperty<Guid>("UserId").HasColumnName("user_id");
                        j.IndexerProperty<Guid>("RoleId").HasColumnName("role_id");
                    });
        });


        // LƯU Ý: Seed data đã được InsertData trực tiếp trong migration file
        // 20260615000000_FullMEPInitialSchema.cs. Toàn bộ seed data đầy đủ
        // (User, Supplier, Product, Customer, Order, ...) sẽ được insert bởi
        // DatabaseSeeder.cs lúc startup vì cần BCrypt hash password thật.

        // SystemNotification fluent API
        modelBuilder.Entity<SystemNotification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("system_notifications_pkey");

            entity.ToTable("system_notifications");

            entity.Property(e => e.NotificationId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("notification_id");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.Target)
                .HasMaxLength(50)
                .HasColumnName("target");
            entity.Property(e => e.BranchId)
                .HasColumnName("branch_id");
            entity.Property(e => e.IsUrgent)
                .HasDefaultValue(false)
                .HasColumnName("is_urgent");
            entity.Property(e => e.ScheduledAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("scheduled_at");
            entity.Property(e => e.SentAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("sent_at");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("DRAFT")
                .HasColumnName("status");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.SystemNotifications)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("system_notifications_created_by_fkey");
        });

        // Notification fluent API (user notifications for inventory check)
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("notifications_pkey");

            entity.ToTable("notifications");

            entity.Property(e => e.NotificationId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("notification_id");
            entity.Property(e => e.Type)
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasColumnName("type");
            entity.Property(e => e.RecipientUserId)
                .HasColumnName("recipient_user_id");
            entity.Property(e => e.BranchId)
                .HasColumnName("branch_id");
            entity.Property(e => e.InventoryCheckId)
                .HasColumnName("inventory_check_id");
            entity.Property(e => e.ReferenceId)
                .HasColumnName("reference_id");
            entity.Property(e => e.TriggeredByUserId)
                .HasColumnName("triggered_by_user_id");
            entity.Property(e => e.Message)
                .HasColumnName("message");
            entity.Property(e => e.IsRead)
                .HasDefaultValue(false)
                .HasColumnName("is_read");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasOne(d => d.RecipientUser)
                .WithMany(p => p.ReceivedNotifications)
                .HasForeignKey(d => d.RecipientUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("notifications_recipient_user_id_fkey");

            entity.HasOne(d => d.TriggeredByUser)
                .WithMany(p => p.TriggeredNotifications)
                .HasForeignKey(d => d.TriggeredByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("notifications_triggered_by_user_id_fkey");

            entity.HasOne(d => d.InventoryCheck)
                .WithMany(p => p.Notifications)
                .HasForeignKey(d => d.InventoryCheckId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("notifications_inventory_check_id_fkey");

            entity.HasOne(d => d.Branch)
                .WithMany(p => p.Notifications)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("notifications_branch_id_fkey");

            entity.Property(e => e.ReferenceId)
                .HasColumnName("reference_id");

            entity.Property(e => e.ReferenceType)
                .HasMaxLength(100)
                .HasColumnName("reference_type");
        });



        // Payment fluent API
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("payments_pkey");
            entity.ToTable("payments");
            entity.Property(e => e.PaymentId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("payment_id");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.Method)
                .HasMaxLength(50)
                .HasColumnName("method");
            entity.Property(e => e.Amount)
                .HasPrecision(15, 2)
                .HasColumnName("amount");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending")
                .HasColumnName("status");
            entity.Property(e => e.CashReceived)
                .HasPrecision(15, 2)
                .HasColumnName("cash_received");
            entity.Property(e => e.ChangeAmount)
                .HasPrecision(15, 2)
                .HasColumnName("change_amount");
            entity.Property(e => e.VietQRString).HasColumnName("viet_qr_string");
            entity.Property(e => e.QRExpiredAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("qr_expired_at");
            entity.Property(e => e.PaidAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("paid_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.ShiftId).HasColumnName("shift_id");
            entity.HasOne(d => d.Invoice).WithMany(p => p.Payments)
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("payments_invoice_id_fkey");
        });

        // Promotion fluent API

        // PromotionUsage fluent API

        // Shift fluent API
        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.ShiftId).HasName("shifts_pkey");
            entity.ToTable("shifts");
            entity.Property(e => e.ShiftId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("shift_id");
            entity.Property(e => e.ShiftCode)
                .HasMaxLength(50)
                .HasColumnName("shift_code");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("OPEN")
                .HasColumnName("status");
            entity.Property(e => e.StartedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("started_at");
            entity.Property(e => e.EndedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ended_at");
            entity.Property(e => e.OpeningBalance)
                .HasPrecision(15, 2)
                .HasColumnName("opening_balance");
            entity.Property(e => e.ExpectedCash)
                .HasPrecision(15, 2)
                .HasColumnName("expected_cash");
            entity.Property(e => e.ActualCash)
                .HasPrecision(15, 2)
                .HasColumnName("actual_cash");
            entity.Property(e => e.Variance)
                .HasPrecision(15, 2)
                .HasColumnName("variance");
            entity.Property(e => e.TotalRevenue)
                .HasPrecision(15, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total_revenue");
            entity.Property(e => e.TotalOrders)
                .HasDefaultValue(0)
                .HasColumnName("total_orders");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.ClosedByUserId).HasColumnName("closed_by_user_id");
            entity.Property(e => e.ForceCloseReason)
                .HasMaxLength(500)
                .HasColumnName("force_close_reason");
            entity.HasIndex(e => e.ShiftCode).IsUnique().HasDatabaseName("ix_shifts_code");
            entity.HasIndex(e => e.BranchId)
                .HasDatabaseName("ix_shifts_one_open_per_branch")
                .HasFilter("status = 'OPEN'")
                .IsUnique();
            entity.HasOne(d => d.User).WithMany(p => p.Shifts)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("shifts_user_id_fkey");
            entity.HasOne(d => d.ClosedBy).WithMany()
                .HasForeignKey(d => d.ClosedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("shifts_closed_by_user_id_fkey");
            entity.HasOne(d => d.Branch).WithMany(p => p.Shifts)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("shifts_branch_id_fkey");
        });

        // LoyaltyTransaction fluent API

        // RefundRecord fluent API
        modelBuilder.Entity<RefundRecord>(entity =>
        {
            entity.HasKey(e => e.RefundId).HasName("refund_records_pkey");
            entity.ToTable("refund_records");
            entity.Property(e => e.RefundId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("refund_id");
            entity.Property(e => e.RefundCode)
                .HasMaxLength(50)
                .HasColumnName("refund_code");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RefundAmount)
                .HasPrecision(15, 2)
                .HasColumnName("refund_amount");
            entity.Property(e => e.RefundMethod)
                .HasMaxLength(50)
                .HasColumnName("refund_method");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("PENDING")
                .HasColumnName("status");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.ShiftId).HasColumnName("shift_id");
            entity.Property(e => e.ProcessedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("processed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.HasIndex(e => e.RefundCode).IsUnique().HasDatabaseName("ix_refund_records_code");
            entity.HasOne(d => d.Order).WithMany()
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("refund_records_order_id_fkey");
            entity.HasOne(d => d.Invoice).WithMany()
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("refund_records_invoice_id_fkey");
            entity.HasOne(d => d.Customer).WithMany()
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("refund_records_customer_id_fkey");
            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("refund_records_user_id_fkey");
        });

        // Receipt fluent API
        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.HasKey(e => e.ReceiptId).HasName("receipts_pkey");
            entity.ToTable("receipts");
            entity.Property(e => e.ReceiptId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("receipt_id");
            entity.Property(e => e.ReceiptCode)
                .HasMaxLength(50)
                .HasColumnName("receipt_code");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.HtmlContent).HasColumnName("html_content");
            entity.Property(e => e.PrintedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("printed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.HasIndex(e => e.ReceiptCode).IsUnique().HasDatabaseName("ix_receipts_code");
            entity.HasOne(d => d.Invoice).WithMany(p => p.Receipts)
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("receipts_invoice_id_fkey");
        });

        // DailySummary fluent API


        OnModelCreatingPartial(modelBuilder);

        // ============================================================
        // EXPENSE MODULE - Fluent API
        // ============================================================
        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("expense_categories_pkey");
            entity.ToTable("expense_categories");

            entity.Property(e => e.CategoryId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("category_id");
            entity.Property(e => e.CategoryName)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("category_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue((short)1)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedBy)
                .HasColumnName("created_by");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasIndex(e => e.CategoryName)
                .IsUnique()
                .HasDatabaseName("expense_categories_category_name_key");

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("expense_categories_created_by_fkey");
        });

        modelBuilder.Entity<ExpenseVoucher>(entity =>
        {
            entity.HasKey(e => e.VoucherId).HasName("expense_vouchers_pkey");
            entity.ToTable("expense_vouchers");

            entity.Property(e => e.VoucherId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("voucher_id");
            entity.Property(e => e.VoucherCode)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("voucher_code");
            entity.Property(e => e.BranchId)
                .HasColumnName("branch_id");
            entity.Property(e => e.CategoryId)
                .HasColumnName("category_id")
                .IsRequired(false);
            entity.Property(e => e.SupplierId)
                .HasColumnName("supplier_id");
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasColumnName("amount");
            entity.Property(e => e.Reason)
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnName("reason");
            entity.Property(e => e.Note)
                .HasColumnName("note");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("PENDING")
                .HasColumnName("status");
            entity.Property(e => e.CreatedBy)
                .HasColumnName("created_by");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.ConfirmedBy)
                .HasColumnName("confirmed_by");
            entity.Property(e => e.CompletedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("completed_at");
            entity.Property(e => e.CancelledBy)
                .HasColumnName("cancelled_by");
            entity.Property(e => e.CancelledAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("cancelled_at");
            entity.Property(e => e.CancelReason)
                .HasMaxLength(500)
                .HasColumnName("cancel_reason");

            entity.HasIndex(e => e.VoucherCode)
                .IsUnique()
                .HasDatabaseName("expense_vouchers_voucher_code_key");

            entity.HasIndex(e => e.BranchId)
                .HasDatabaseName("ix_expense_vouchers_branch_id");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("ix_expense_vouchers_status");

            entity.HasIndex(e => e.CategoryId)
                .HasDatabaseName("ix_expense_vouchers_category_id");

            entity.HasOne(d => d.Branch)
                .WithMany()
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("expense_vouchers_branch_id_fkey");

            entity.HasOne(d => d.Category)
                .WithMany(p => p.ExpenseVouchers)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("expense_vouchers_category_id_fkey");

            entity.HasOne(d => d.Supplier)
                .WithMany()
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("expense_vouchers_supplier_id_fkey");

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("expense_vouchers_created_by_fkey");

            entity.HasOne(d => d.ConfirmedByUser)
                .WithMany()
                .HasForeignKey(d => d.ConfirmedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("expense_vouchers_confirmed_by_fkey");

            entity.HasOne(d => d.CancelledByUser)
                .WithMany()
                .HasForeignKey(d => d.CancelledBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("expense_vouchers_cancelled_by_fkey");
        });

        // ============================================================
        // ORDER DISCOUNT TIER - Chiết khấu theo tổng giá trị đơn hàng
        // ============================================================
        modelBuilder.Entity<OrderDiscountTier>(entity =>
        {
            entity.HasKey(e => e.OrderDiscountTierId).HasName("order_discount_tiers_pkey");
            entity.ToTable("order_discount_tiers");

            entity.Property(e => e.OrderDiscountTierId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("order_discount_tier_id");
            entity.Property(e => e.BranchId)
                .HasColumnName("branch_id");
            entity.Property(e => e.MinOrderValue)
                .IsRequired()
                .HasPrecision(18, 2)
                .HasColumnName("min_order_value");
            entity.Property(e => e.DiscountPercent)
                .IsRequired()
                .HasPrecision(5, 2)
                .HasColumnName("discount_percent");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
        });

        // ============================================================
        // PRODUCT ATTRIBUTE TYPE - Loại thuộc tính sản phẩm (kim khí điện nước)
        // ============================================================
        modelBuilder.Entity<ProductAttributeType>(entity =>
        {
            entity.HasKey(e => e.TypeId).HasName("product_attribute_types_pkey");
            entity.ToTable("product_attribute_types");

            entity.Property(e => e.TypeId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("type_id");
            entity.Property(e => e.TypeName)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("type_name");
            entity.Property(e => e.TypeCode)
                .HasMaxLength(50)
                .HasColumnName("type_code");
            entity.Property(e => e.IsActive)
                .HasDefaultValue((short)1)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasColumnName("updated_by");

            entity.HasIndex(e => e.TypeName)
                .IsUnique()
                .HasDatabaseName("ix_product_attribute_types_type_name");
        });

        // PRODUCT LOCATION - Vị trí lưu trữ sản phẩm
        // ============================================================
        modelBuilder.Entity<ProductLocation>(entity =>
        {
            entity.HasKey(e => e.LocationId).HasName("product_locations_pkey");
            entity.ToTable("product_locations");

            entity.Property(e => e.LocationId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("location_id");
            entity.Property(e => e.LocationName)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("location_name");
            entity.Property(e => e.LocationCode)
                .HasMaxLength(50)
                .HasColumnName("location_code");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue((short)1)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasColumnName("updated_by");

            entity.HasIndex(e => e.LocationName)
                .IsUnique()
                .HasDatabaseName("ix_product_locations_location_name");
        });

        // ============================================================
        // STOCK PRINT TEMPLATE
        // ============================================================
        modelBuilder.Entity<StockPrintTemplate>(entity =>
        {
            entity.HasKey(e => e.TemplateId).HasName("stock_print_templates_pkey");
            entity.ToTable("stock_print_templates");

            entity.Property(e => e.TemplateId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("template_id");
            entity.Property(e => e.BranchId)
                .HasColumnName("branch_id");
            entity.Property(e => e.TicketType)
                .HasMaxLength(50)
                .HasColumnName("ticket_type");
            entity.Property(e => e.HeaderText)
                .HasMaxLength(500)
                .HasColumnName("header_text");
            entity.Property(e => e.FooterText)
                .HasMaxLength(500)
                .HasColumnName("footer_text");
            entity.Property(e => e.ShowLogo)
                .HasDefaultValue(false)
                .HasColumnName("show_logo");
            entity.Property(e => e.LogoUrl)
                .HasMaxLength(500)
                .HasColumnName("logo_url");
            entity.Property(e => e.BranchName)
                .HasMaxLength(200)
                .HasColumnName("branch_name");
            entity.Property(e => e.BranchAddress)
                .HasMaxLength(500)
                .HasColumnName("branch_address");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .HasColumnName("tax_code");
            entity.Property(e => e.PaperSize)
                .HasMaxLength(20)
                .HasDefaultValue("A5")
                .HasColumnName("paper_size");
            entity.Property(e => e.FontSize)
                .HasDefaultValue(12)
                .HasColumnName("font_size");
            entity.Property(e => e.FontFamily)
                .HasMaxLength(50)
                .HasDefaultValue("monospace")
                .HasColumnName("font_family");
            entity.Property(e => e.ShowSupplier)
                .HasDefaultValue(true)
                .HasColumnName("show_supplier");
            entity.Property(e => e.ShowBranchInfo)
                .HasDefaultValue(true)
                .HasColumnName("show_branch_info");
            entity.Property(e => e.ShowSignature)
                .HasDefaultValue(true)
                .HasColumnName("show_signature");
            entity.Property(e => e.CustomFieldsJson)
                .HasColumnType("text")
                .HasDefaultValue("{}")
                .HasColumnName("custom_fields_json");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            // Unique index (BranchId, TicketType) — both nullable so use IsUnique with filter
            entity.HasIndex(e => new { e.BranchId, e.TicketType })
                .IsUnique()
                .HasDatabaseName("ix_stock_print_templates_branch_ticket");

            entity.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ============================================================
        // STOCK TICKET RECEIPT (HTML snapshot when printing stock tickets)
        // ============================================================
        modelBuilder.Entity<StockTicketReceipt>(entity =>
        {
            entity.HasKey(e => e.ReceiptId).HasName("stock_ticket_receipts_pkey");
            entity.ToTable("stock_ticket_receipts");

            entity.Property(e => e.ReceiptId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("receipt_id");
            entity.Property(e => e.ReceiptCode)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("receipt_code");
            entity.Property(e => e.StockTicketId)
                .HasColumnName("stock_ticket_id");
            entity.Property(e => e.HtmlContent)
                .HasColumnType("text")
                .HasColumnName("html_content");
            entity.Property(e => e.PrintedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("printed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasIndex(e => e.ReceiptCode)
                .IsUnique()
                .HasDatabaseName("ix_stock_ticket_receipts_code");

            entity.HasOne(e => e.StockTicket)
                .WithMany()
                .HasForeignKey(e => e.StockTicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // INVOICE PRINT TEMPLATE
        // ============================================================
        modelBuilder.Entity<InvoicePrintTemplate>(entity =>
        {
            entity.HasKey(e => e.TemplateId).HasName("invoice_print_templates_pkey");
            entity.ToTable("invoice_print_templates");

            entity.Property(e => e.TemplateId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("template_id");
            entity.Property(e => e.BranchId)
                .HasColumnName("branch_id");
            entity.Property(e => e.HeaderText)
                .HasMaxLength(500)
                .HasColumnName("header_text");
            entity.Property(e => e.FooterText)
                .HasMaxLength(500)
                .HasColumnName("footer_text");
            entity.Property(e => e.ShowLogo)
                .HasDefaultValue(false)
                .HasColumnName("show_logo");
            entity.Property(e => e.LogoUrl)
                .HasMaxLength(500)
                .HasColumnName("logo_url");
            entity.Property(e => e.BranchName)
                .HasMaxLength(200)
                .HasColumnName("branch_name");
            entity.Property(e => e.BranchAddress)
                .HasMaxLength(500)
                .HasColumnName("branch_address");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .HasColumnName("tax_code");
            entity.Property(e => e.PaperSize)
                .HasMaxLength(20)
                .HasDefaultValue("K80")
                .HasColumnName("paper_size");
            entity.Property(e => e.FontSize)
                .HasDefaultValue(12)
                .HasColumnName("font_size");
            entity.Property(e => e.FontFamily)
                .HasMaxLength(50)
                .HasDefaultValue("monospace")
                .HasColumnName("font_family");
            entity.Property(e => e.ShowCustomerInfo)
                .HasDefaultValue(true)
                .HasColumnName("show_customer_info");
            entity.Property(e => e.ShowCashierName)
                .HasDefaultValue(true)
                .HasColumnName("show_cashier_name");
            entity.Property(e => e.ShowBranchInfo)
                .HasDefaultValue(true)
                .HasColumnName("show_branch_info");
            entity.Property(e => e.ShowPaymentMethod)
                .HasDefaultValue(true)
                .HasColumnName("show_payment_method");
            entity.Property(e => e.ThankYouMessage)
                .HasMaxLength(500)
                .HasColumnName("thank_you_message");
            entity.Property(e => e.CustomFieldsJson)
                .HasColumnType("text")
                .HasDefaultValue("{}")
                .HasColumnName("custom_fields_json");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ============================================================
        // PASSWORD RESET TOKEN
        // ============================================================
        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("password_reset_tokens_pkey");
            entity.ToTable("password_reset_tokens");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.UserId)
                .HasColumnName("user_id");
            entity.Property(e => e.TokenHash)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("token_hash");
            entity.Property(e => e.ExpiresAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("expires_at");
            entity.Property(e => e.UsedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("used_at");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(50)
                .HasColumnName("ip_address");
            entity.Property(e => e.UserAgent)
                .HasMaxLength(255)
                .HasColumnName("user_agent");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasIndex(e => e.TokenHash)
                .IsUnique()
                .HasDatabaseName("ix_password_reset_tokens_token_hash");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Fix OrderDiscountTier unique index: unique (BranchId, MinOrderValue)
        modelBuilder.Entity<OrderDiscountTier>()
            .HasIndex(e => new { e.BranchId, e.MinOrderValue })
            .IsUnique()
            .HasDatabaseName("ix_order_discount_tiers_branch_min_order");

        // === Inventory Ledger (Nhật ký biến động kho) ===
        modelBuilder.Entity<InventoryLedger>(entity =>
        {
            entity.HasKey(e => e.LedgerId).HasName("inventory_ledger_pkey");
            entity.ToTable("inventory_ledger");

            entity.Property(e => e.LedgerId).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("ledger_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.BranchProductId).HasColumnName("branch_product_id");
            entity.Property(e => e.DocType).HasMaxLength(20).HasColumnName("doc_type");
            entity.Property(e => e.DocId).HasColumnName("doc_id");
            entity.Property(e => e.DocCode).HasMaxLength(50).HasColumnName("doc_code");
            entity.Property(e => e.TransactionTime).HasColumnType("timestamp without time zone").HasColumnName("transaction_time");
            entity.Property(e => e.QtyChange).HasPrecision(10, 2).HasColumnName("qty_change");
            entity.Property(e => e.UnitCostPrice).HasPrecision(18, 4).HasColumnName("unit_cost_price");
            entity.Property(e => e.TotalCostValue).HasPrecision(18, 4).HasColumnName("total_cost_value");
            entity.Property(e => e.StockAfter).HasPrecision(10, 2).HasColumnName("stock_after");
            entity.Property(e => e.AvgCostAfter).HasPrecision(18, 4).HasColumnName("avg_cost_after");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasIndex(e => new { e.BranchId, e.BranchProductId, e.TransactionTime }).HasDatabaseName("ix_ledger_branch_product_time");
            entity.HasIndex(e => new { e.BranchId, e.TransactionTime }).HasDatabaseName("ix_ledger_branch_time");
        });

        // === Daily Stock Snapshots (Chốt tồn cuối ngày) ===
        modelBuilder.Entity<DailyStockSnapshot>(entity =>
        {
            entity.HasKey(e => e.SnapshotId).HasName("daily_stock_snapshots_pkey");
            entity.ToTable("daily_stock_snapshots");

            entity.Property(e => e.SnapshotId).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("snapshot_id");
            entity.Property(e => e.SnapshotDate).HasColumnName("snapshot_date");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.BranchProductId).HasColumnName("branch_product_id");
            entity.Property(e => e.ClosingQuantity).HasPrecision(10, 2).HasColumnName("closing_quantity");
            entity.Property(e => e.CostPrice).HasPrecision(18, 4).HasColumnName("cost_price");
            entity.Property(e => e.ClosingValue).HasPrecision(18, 4).HasColumnName("closing_value");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasIndex(e => new { e.SnapshotDate, e.BranchId, e.BranchProductId }).IsUnique().HasDatabaseName("uk_daily_snapshot");
        });

        // === Store Approvals (Yêu cầu đăng ký cửa hàng chờ Admin duyệt) ===
        modelBuilder.Entity<StoreApproval>(entity =>
        {
            entity.HasKey(e => e.ApprovalId).HasName("store_approvals_pkey");
            entity.ToTable("store_approvals");

            entity.Property(e => e.ApprovalId).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("approval_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.StoreName).HasMaxLength(255).HasColumnName("store_name");
            entity.Property(e => e.BusinessLicense).HasMaxLength(255).HasColumnName("business_license");
            entity.Property(e => e.TaxCode).HasMaxLength(100).HasColumnName("tax_code");
            entity.Property(e => e.StoreAddress).HasColumnName("store_address");
            entity.Property(e => e.DocumentUrls).HasColumnName("document_urls");
            entity.Property(e => e.Status).HasMaxLength(50).HasColumnName("status");
            entity.Property(e => e.RejectReason).HasColumnName("reject_reason");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.ReviewedAt).HasColumnType("timestamp without time zone").HasColumnName("reviewed_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasIndex(e => e.UserId).HasDatabaseName("ix_store_approvals_user");
            entity.HasIndex(e => e.Status).HasDatabaseName("ix_store_approvals_status");
        });

        // === Goods Issues (Phiếu xuất kho nội bộ) ===
        modelBuilder.Entity<GoodsIssue>(entity =>
        {
            entity.HasKey(e => e.GoodsIssueId).HasName("goods_issues_pkey");
            entity.ToTable("goods_issues");

            entity.Property(e => e.GoodsIssueId).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("goods_issue_id");
            entity.Property(e => e.IssueNumber).HasMaxLength(100).HasColumnName("issue_number");
            entity.Property(e => e.IssueType).HasMaxLength(100).HasColumnName("issue_type");
            entity.Property(e => e.CustomerId).HasMaxLength(100).HasColumnName("customer_id");
            entity.Property(e => e.CustomerName).HasMaxLength(255).HasColumnName("customer_name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Reference).HasMaxLength(255).HasColumnName("reference");
            entity.Property(e => e.IssueDate).HasColumnType("timestamp without time zone").HasColumnName("issue_date");
            entity.Property(e => e.CreatedBy).HasMaxLength(255).HasColumnName("created_by");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.SyncStatus).HasMaxLength(50).HasColumnName("sync_status");
            entity.Property(e => e.SyncNumber).HasMaxLength(100).HasColumnName("sync_number");
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2).HasColumnName("total_amount");
            entity.Property(e => e.TotalPayment).HasPrecision(18, 2).HasColumnName("total_payment");
            entity.Property(e => e.PaymentMethod).HasMaxLength(50).HasColumnName("payment_method");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.Status).HasMaxLength(50).HasColumnName("status");
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .HasConstraintName("fk_goods_issues_branch");

            entity.HasIndex(e => e.BranchId).HasDatabaseName("ix_goods_issues_branch");
            entity.HasIndex(e => e.IssueNumber).HasDatabaseName("ix_goods_issues_number");
        });

        // === Goods Issue Items (Dòng hàng của phiếu xuất kho) ===
        modelBuilder.Entity<GoodsIssueItem>(entity =>
        {
            entity.HasKey(e => e.GoodsIssueItemId).HasName("goods_issue_items_pkey");
            entity.ToTable("goods_issue_items");

            entity.Property(e => e.GoodsIssueItemId).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("goods_issue_item_id");
            entity.Property(e => e.GoodsIssueId).HasColumnName("goods_issue_id");
            entity.Property(e => e.LineNumber).HasColumnName("line_number");
            entity.Property(e => e.ProductId).HasMaxLength(100).HasColumnName("product_id");
            entity.Property(e => e.ProductCode).HasMaxLength(100).HasColumnName("product_code");
            entity.Property(e => e.ProductName).HasMaxLength(500).HasColumnName("product_name");
            entity.Property(e => e.Unit).HasMaxLength(50).HasColumnName("unit");
            entity.Property(e => e.LotNumber).HasMaxLength(100).HasColumnName("lot_number");
            entity.Property(e => e.ExpiryDate).HasMaxLength(50).HasColumnName("expiry_date");
            entity.Property(e => e.WarehouseId).HasMaxLength(100).HasColumnName("warehouse_id");
            entity.Property(e => e.WarehouseName).HasMaxLength(255).HasColumnName("warehouse_name");
            entity.Property(e => e.Location).HasMaxLength(255).HasColumnName("location");
            entity.Property(e => e.Quantity).HasPrecision(18, 3).HasColumnName("quantity");
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2).HasColumnName("unit_price");
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2).HasColumnName("total_amount");

            entity.HasOne(e => e.GoodsIssue)
                .WithMany(e => e.Items)
                .HasForeignKey(e => e.GoodsIssueId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_goods_issue_items_issue");

            entity.HasIndex(e => e.GoodsIssueId).HasDatabaseName("ix_goods_issue_items_issue");
        });

        // === Goods Issue Attachments (File đính kèm phiếu xuất kho) ===
        modelBuilder.Entity<GoodsIssueAttachment>(entity =>
        {
            entity.HasKey(e => e.GoodsIssueAttachmentId).HasName("goods_issue_attachments_pkey");
            entity.ToTable("goods_issue_attachments");

            entity.Property(e => e.GoodsIssueAttachmentId).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("goods_issue_attachment_id");
            entity.Property(e => e.GoodsIssueId).HasColumnName("goods_issue_id");
            entity.Property(e => e.FileName).HasMaxLength(500).HasColumnName("file_name");
            entity.Property(e => e.ContentType).HasMaxLength(255).HasColumnName("content_type");
            entity.Property(e => e.Size).HasColumnName("size");
            entity.Property(e => e.MediaUrl).HasColumnName("media_url");
            entity.Property(e => e.UploadedAt).HasColumnType("timestamp without time zone").HasColumnName("uploaded_at");
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");

            entity.HasOne(e => e.GoodsIssue)
                .WithMany(e => e.Attachments)
                .HasForeignKey(e => e.GoodsIssueId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_goods_issue_attachments_issue");

            entity.HasIndex(e => e.GoodsIssueId).HasDatabaseName("ix_goods_issue_attachments_issue");
        });
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}


