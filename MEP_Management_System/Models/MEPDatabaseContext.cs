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

    public virtual DbSet<Comment> Comments { get; set; }

    public virtual DbSet<CommunityCategory> CommunityCategories { get; set; }

    public virtual DbSet<CommunityPost> CommunityPosts { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoiceItem> InvoiceItems { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<MediaResource> MediaResources { get; set; }

    public virtual DbSet<OtpVerification> OtpVerifications { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductAttribute> ProductAttributes { get; set; }

    public virtual DbSet<ProductConversionUnit> ProductConversionUnits { get; set; }

    public virtual DbSet<PurchaseOrder> PurchaseOrders { get; set; }

    public virtual DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }

    public virtual DbSet<ReturnItem> ReturnItems { get; set; }

    public virtual DbSet<ReturnOrder> ReturnOrders { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<StaffAssignment> StaffAssignments { get; set; }

    public virtual DbSet<StockTicket> StockTickets { get; set; }

    public virtual DbSet<StockTicketItem> StockTicketItems { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<SupplierRating> SupplierRatings { get; set; }

    public virtual DbSet<SystemConfig> SystemConfigs { get; set; }

    public virtual DbSet<StoreApproval> StoreApprovals { get; set; }

    public virtual DbSet<SystemLog> SystemLogs { get; set; }

    public virtual DbSet<SystemNotification> SystemNotifications { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Promotion> Promotions { get; set; }

    public virtual DbSet<PromotionUsage> PromotionUsages { get; set; }

    public virtual DbSet<Shift> Shifts { get; set; }

    public virtual DbSet<LoyaltyTransaction> LoyaltyTransactions { get; set; }

    public virtual DbSet<RefundRecord> RefundRecords { get; set; }

    public virtual DbSet<Receipt> Receipts { get; set; }

    public virtual DbSet<DailySummary> DailySummaries { get; set; }

    public virtual DbSet<ViolationReport> ViolationReports { get; set; }

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
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .HasColumnName("address");
            entity.Property(e => e.BranchName)
                .HasMaxLength(100)
                .HasColumnName("branch_name");
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
                .HasDefaultValue((short)1)
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

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("comments_pkey");

            entity.ToTable("comments");

            entity.Property(e => e.CommentId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("comment_id");
            entity.Property(e => e.CommentText).HasColumnName("comment_text");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.PostId).HasColumnName("post_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Post).WithMany(p => p.Comments)
                .HasForeignKey(d => d.PostId)
                .HasConstraintName("comments_post_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Comments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("comments_user_id_fkey");
        });

        modelBuilder.Entity<CommunityCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("community_categories_pkey");

            entity.ToTable("community_categories");

            entity.Property(e => e.CategoryId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("category_id");
            entity.Property(e => e.CategoryName)
                .HasMaxLength(100)
                .HasColumnName("category_name");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.IsArchived)
                .HasDefaultValue(false)
                .HasColumnName("is_archived");
            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0)
                .HasColumnName("sort_order");
            entity.Property(e => e.Slug)
                .HasMaxLength(255)
                .HasColumnName("slug");
            entity.HasIndex(e => e.Slug).IsUnique().HasDatabaseName("ix_community_categories_slug");
            entity.Property(e => e.ParentId)
                .HasColumnName("parent_id");

            entity.HasOne(d => d.Parent).WithMany(p => p.Children)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("community_categories_parent_id_fkey");
        });

        modelBuilder.Entity<CommunityPost>(entity =>
        {
            entity.HasKey(e => e.PostId).HasName("community_posts_pkey");

            entity.ToTable("community_posts");

            entity.Property(e => e.PostId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("post_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("status");
            entity.Property(e => e.IsPinned)
                .HasDefaultValue((short)0)
                .HasColumnName("is_pinned");
            entity.Property(e => e.ViewCount)
                .HasDefaultValue(0)
                .HasColumnName("view_count");
            entity.Property(e => e.GroupBuyingTarget)
                .HasColumnName("group_buying_target");
            entity.Property(e => e.GroupBuyingPrice)
                .HasPrecision(15, 2)
                .HasColumnName("group_buying_price");

            entity.HasOne(d => d.Category).WithMany(p => p.CommunityPosts)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("community_posts_category_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.CommunityPosts)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("community_posts_user_id_fkey");
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
            entity.Property(e => e.InvoiceCode)
                .HasMaxLength(50)
                .HasColumnName("invoice_code");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.Note).HasColumnName("note");

            entity.HasIndex(e => e.InvoiceCode, "invoices_invoice_code_key").IsUnique();

            entity.HasOne(d => d.Order).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("invoices_order_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("invoices_user_id_fkey");
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
                .HasMaxLength(255)
                .HasColumnName("media_url");
            entity.Property(e => e.PostId).HasColumnName("post_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");

            entity.HasOne(d => d.Post).WithMany(p => p.MediaResources)
                .HasForeignKey(d => d.PostId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("media_resources_post_id_fkey");

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
        });

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
            entity.Property(e => e.Quantity)
                .HasPrecision(10, 2)
                .HasColumnName("quantity");
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

            entity.HasIndex(e => e.TicketCode, "stock_tickets_ticket_code_key").IsUnique();

            entity.HasOne(d => d.Branch).WithMany(p => p.StockTickets)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("stock_tickets_branch_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.StockTickets)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("stock_tickets_user_id_fkey");
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
            entity.Property(e => e.SystemQuantity)
                .HasPrecision(10, 2)
                .HasColumnName("system_quantity");

            entity.HasOne(d => d.BranchProduct).WithMany(p => p.StockTicketItems)
                .HasForeignKey(d => d.BranchProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("stock_ticket_items_branch_product_id_fkey");

            entity.HasOne(d => d.StockTicket).WithMany(p => p.StockTicketItems)
                .HasForeignKey(d => d.StockTicketId)
                .HasConstraintName("stock_ticket_items_stock_ticket_id_fkey");
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
        });

        modelBuilder.Entity<SupplierRating>(entity =>
        {
            entity.HasKey(e => e.RatingId).HasName("supplier_ratings_pkey");

            entity.ToTable("supplier_ratings");

            entity.Property(e => e.RatingId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("rating_id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.PurchaseOrderId).HasColumnName("purchase_order_id");
            entity.Property(e => e.QualityScore).HasColumnName("quality_score");
            entity.Property(e => e.DeliveryScore).HasColumnName("delivery_score");
            entity.Property(e => e.PriceScore).HasColumnName("price_score");
            entity.Property(e => e.ServiceScore).HasColumnName("service_score");
            entity.Property(e => e.OverallRating)
                .HasPrecision(3, 2)
                .HasColumnName("overall_rating");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.RatedBy).HasColumnName("rated_by");
            entity.Property(e => e.RatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("rated_at");

            entity.HasIndex(e => e.SupplierId, "ix_supplier_ratings_supplier_id");
            entity.HasIndex(e => e.RatedAt, "ix_supplier_ratings_rated_at");

            entity.HasOne(d => d.Supplier).WithMany(p => p.SupplierRatings)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("supplier_ratings_supplier_id_fkey");

            entity.HasOne(d => d.PurchaseOrder).WithMany()
                .HasForeignKey(d => d.PurchaseOrderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("supplier_ratings_po_id_fkey");

            entity.HasOne(d => d.RatedByUser).WithMany()
                .HasForeignKey(d => d.RatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("supplier_ratings_rated_by_fkey");
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

        modelBuilder.Entity<ViolationReport>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("violation_reports_pkey");

            entity.ToTable("violation_reports");

            entity.Property(e => e.ReportId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("report_id");
            entity.Property(e => e.CommentId).HasColumnName("comment_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.PostId).HasColumnName("post_id");
            entity.Property(e => e.Reason)
                .HasMaxLength(255)
                .HasColumnName("reason");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'PENDING'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ResolvedBy).HasColumnName("resolved_by");
            entity.Property(e => e.ResolvedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("resolved_at");
            entity.Property(e => e.AdminNote).HasColumnName("admin_note");

            entity.HasOne(d => d.Comment).WithMany(p => p.ViolationReports)
                .HasForeignKey(d => d.CommentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("violation_reports_comment_id_fkey");

            entity.HasOne(d => d.Post).WithMany(p => p.ViolationReports)
                .HasForeignKey(d => d.PostId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("violation_reports_post_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.ViolationReports)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("violation_reports_user_id_fkey");

            entity.HasOne(d => d.ResolvedByUser).WithMany()
                .HasForeignKey(d => d.ResolvedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("violation_reports_resolved_by_fkey");
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

        modelBuilder.Entity<StoreApproval>(entity =>
        {
            entity.HasKey(e => e.ApprovalId).HasName("store_approvals_pkey");

            entity.ToTable("store_approvals");

            entity.Property(e => e.ApprovalId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("approval_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.StoreName)
                .HasMaxLength(200)
                .HasColumnName("store_name");
            entity.Property(e => e.BusinessLicense)
                .HasMaxLength(100)
                .HasColumnName("business_license");
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .HasColumnName("tax_code");
            entity.Property(e => e.StoreAddress)
                .HasMaxLength(255)
                .HasColumnName("store_address");
            entity.Property(e => e.DocumentUrls).HasColumnName("document_urls");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("PENDING")
                .HasColumnName("status");
            entity.Property(e => e.RejectReason)
                .HasMaxLength(500)
                .HasColumnName("reject_reason");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.ReviewedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("reviewed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasOne(d => d.User).WithMany(p => p.StoreApprovals)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("store_approvals_user_id_fkey");

            entity.HasOne(d => d.ReviewedByUser).WithMany()
                .HasForeignKey(d => d.ReviewedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("store_approvals_reviewed_by_fkey");
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
            entity.HasOne(d => d.Invoice).WithMany(p => p.Payments)
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("payments_invoice_id_fkey");
        });

        // Promotion fluent API
        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(e => e.PromotionId).HasName("promotions_pkey");
            entity.ToTable("promotions");
            entity.Property(e => e.PromotionId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("promotion_id");
            entity.Property(e => e.PromotionCode)
                .HasMaxLength(50)
                .HasColumnName("promotion_code");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DiscountType)
                .HasMaxLength(50)
                .HasColumnName("discount_type");
            entity.Property(e => e.DiscountValue)
                .HasPrecision(15, 2)
                .HasColumnName("discount_value");
            entity.Property(e => e.MinOrderAmount)
                .HasPrecision(15, 2)
                .HasColumnName("min_order_amount");
            entity.Property(e => e.MaxDiscountAmount)
                .HasPrecision(15, 2)
                .HasColumnName("max_discount_amount");
            entity.Property(e => e.StartDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("start_date");
            entity.Property(e => e.EndDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("end_date");
            entity.Property(e => e.UsageLimit).HasColumnName("usage_limit");
            entity.Property(e => e.UsedCount)
                .HasDefaultValue(0)
                .HasColumnName("used_count");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.HasIndex(e => e.PromotionCode).IsUnique().HasDatabaseName("ix_promotions_code");
        });

        // PromotionUsage fluent API
        modelBuilder.Entity<PromotionUsage>(entity =>
        {
            entity.HasKey(e => e.UsageId).HasName("promotion_usages_pkey");
            entity.ToTable("promotion_usages");
            entity.Property(e => e.UsageId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("usage_id");
            entity.Property(e => e.PromotionId).HasColumnName("promotion_id");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.DiscountAmount)
                .HasPrecision(15, 2)
                .HasColumnName("discount_amount");
            entity.Property(e => e.UsedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("used_at");
            entity.HasOne(d => d.Promotion).WithMany(p => p.PromotionUsages)
                .HasForeignKey(d => d.PromotionId)
                .HasConstraintName("promotion_usages_promotion_id_fkey");
            entity.HasOne(d => d.Invoice).WithMany()
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("promotion_usages_invoice_id_fkey");
            entity.HasOne(d => d.Customer).WithMany()
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("promotion_usages_customer_id_fkey");
            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("promotion_usages_user_id_fkey");
        });

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
            entity.HasIndex(e => e.ShiftCode).IsUnique().HasDatabaseName("ix_shifts_code");
            entity.HasOne(d => d.User).WithMany(p => p.Shifts)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("shifts_user_id_fkey");
            entity.HasOne(d => d.Branch).WithMany(p => p.Shifts)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("shifts_branch_id_fkey");
        });

        // LoyaltyTransaction fluent API
        modelBuilder.Entity<LoyaltyTransaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId).HasName("loyalty_transactions_pkey");
            entity.ToTable("loyalty_transactions");
            entity.Property(e => e.TransactionId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("transaction_id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.Points).HasColumnName("points");
            entity.Property(e => e.TransactionType)
                .HasMaxLength(50)
                .HasColumnName("transaction_type");
            entity.Property(e => e.ReferenceType)
                .HasMaxLength(50)
                .HasColumnName("reference_type");
            entity.Property(e => e.ReferenceId).HasColumnName("reference_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.HasOne(d => d.Customer).WithMany(p => p.LoyaltyTransactions)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("loyalty_transactions_customer_id_fkey");
        });

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
        modelBuilder.Entity<DailySummary>(entity =>
        {
            entity.HasKey(e => e.SummaryId).HasName("daily_summaries_pkey");
            entity.ToTable("daily_summaries");
            entity.Property(e => e.SummaryId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("summary_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.SummaryDate)
                .HasColumnType("date")
                .HasColumnName("summary_date");
            entity.Property(e => e.TotalRevenue)
                .HasPrecision(15, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total_revenue");
            entity.Property(e => e.TotalOrders)
                .HasDefaultValue(0)
                .HasColumnName("total_orders");
            entity.Property(e => e.TotalRefunds)
                .HasPrecision(15, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total_refunds");
            entity.Property(e => e.TotalCash)
                .HasPrecision(15, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total_cash");
            entity.Property(e => e.TotalTransfer)
                .HasPrecision(15, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total_transfer");
            entity.Property(e => e.TotalDebt)
                .HasPrecision(15, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total_debt");
            entity.Property(e => e.OpeningCash)
                .HasPrecision(15, 2)
                .HasDefaultValue(0m)
                .HasColumnName("opening_cash");
            entity.Property(e => e.ClosingCash)
                .HasPrecision(15, 2)
                .HasDefaultValue(0m)
                .HasColumnName("closing_cash");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.HasOne(d => d.Branch).WithMany(p => p.DailySummaries)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("daily_summaries_branch_id_fkey");
        });


        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
