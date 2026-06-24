using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.InventoryDTOs
{
    #region Product DTOs
    public class ProductUpsertDto
    {
        public Guid? ProductId { get; set; }
        public string? Id { get; set; }
        public string ProductCode { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public string? Barcode { get; set; }
        public string Unit { get; set; } = null!;
        public string? BrandName { get; set; }
        public string? CategoryName { get; set; }
        public Guid? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? ItemType { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SalePrice { get; set; }
        public decimal ActualStock { get; set; }
        public decimal ReservedStock { get; set; }
        public decimal AvailableStock { get; set; }
        public decimal? MinimumStock { get; set; }
        public decimal? MaximumStock { get; set; }
        public string? ShelfLocation { get; set; }
        public List<string> ShelfLocations { get; set; } = new();
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public decimal? Width { get; set; }
        public decimal? Length { get; set; }
        public decimal? Height { get; set; }
        public string? Specification { get; set; }
        public string? SpecificationDetail { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> Images { get; set; } = new();
        public bool DirectSale { get; set; }
        public bool SalesChannelLinked { get; set; }
        public bool IsActive { get; set; }
        public List<ProductAttributeDto> Attributes { get; set; } = new();
        public List<ConversionUnitDto> ConversionUnits { get; set; } = new();
        public DateTime? EstimatedOutAt { get; set; }

        // --- CÁC TRƯỜNG CŨ CỦA BẠN ĐÃ CÓ ---
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Status { get; set; }
        public string? StatusTone { get; set; }
        public string? ProductStatus { get; set; }

        // 🎯 THÊM MỚI 4 TRƯỜNG NÀY VÀO ĐÂY:
        public Guid? CreatedBy { get; set; }
        public string? CreatedByName { get; set; } // Tên người tạo sản phẩm hiển thị trên giao diện
        public Guid? UpdatedBy { get; set; }
        public string? UpdatedByName { get; set; } // Tên người sửa sản phẩm hiển thị trên giao diện
    }

    public class ProductAttributeDto
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = null!;
        public string Value { get; set; } = null!;
    }

    public class ConversionUnitDto
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Rate { get; set; }
        public string? Note { get; set; }
        public decimal ConvertValue { get; set; }
        public string? ConvertFrom { get; set; }
        public decimal Price { get; set; }
        public bool DirectSale { get; set; }
    }

    public class ProductMediaDto
    {
        public string MediaUrl { get; set; } = null!;
        public string MediaType { get; set; } = "IMAGE";
        public short IsPrimary { get; set; } = 0;
    }

    public class CreateProductDto
    {
        public string ProductCode { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public string? Barcode { get; set; }
        public string BaseUnit { get; set; } = null!;
        public string? Specification { get; set; }
        public string? ItemType { get; set; }
        public string? BrandName { get; set; }
        public string? CategoryName { get; set; }
        public Guid? SupplierId { get; set; }
        public List<ProductMediaDto> Medias { get; set; } = new();
    }

    public class UpdateProductDto
    {
        public string ProductName { get; set; } = null!;
        public string? Barcode { get; set; }
        public string? Specification { get; set; }
        public string? ItemType { get; set; }
        public string? BrandName { get; set; }
        public string? CategoryName { get; set; }
        public Guid? SupplierId { get; set; }
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public decimal? Width { get; set; }
        public decimal? Length { get; set; }
        public decimal? Height { get; set; }
        public string? SizeUnit { get; set; }
        public List<ProductMediaDto> Medias { get; set; } = new();
    }

    public class ProductParameters
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SearchTerm { get; set; }
        public string? Sort { get; set; }
        public string? Order { get; set; } = "asc";
        public string? CategoryName { get; set; }
        public string? BrandName { get; set; }
        public string? Status { get; set; }
        public Guid? SupplierId { get; set; }
    }
    #endregion

    #region Outward Inventory DTOs
    public enum OutwardInventoryType
    {
        ReturnToSupplier = 1,
        DamagedHaoHut = 2,
        InternalUse = 3
    }

    public class OutwardInventoryItemDto
    {
        public Guid BranchProductId { get; set; }
        public decimal Quantity { get; set; }
    }

    public class OutwardInventoryCreateDto
    {
        public OutwardInventoryType OutwardType { get; set; }
        public string Reason { get; set; } = null!;
        public string? Note { get; set; }
        public List<OutwardInventoryItemDto> Items { get; set; } = new();
    }

    public class OutwardInventoryUpdateDto
    {
        public string? Reason { get; set; }
        public string? Status { get; set; }
    }

    public class OutwardInventoryItemResponseDto
    {
        public Guid TicketItemId { get; set; }
        public Guid BranchProductId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductCode { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public decimal SystemQuantity { get; set; }
        public decimal Quantity { get; set; }
        public decimal ActualQuantity { get; set; }
    }

    public class OutwardInventoryResponseDto
    {
        public Guid StockTicketId { get; set; }
        public string TicketCode { get; set; } = null!;
        public string TicketType { get; set; } = null!;
        public string Status { get; set; } = null!;
        public Guid BranchId { get; set; }
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public string? Reason { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<OutwardInventoryItemResponseDto> Items { get; set; } = new();
    }
    #endregion

    #region Inward Inventory DTOs
    public enum InwardInventoryType
    {
        Purchase = 1,
        CustomerReturn = 2,
        BalanceAdjust = 3
    }

    public class InwardInventoryItemDto
    {
        /// <summary>
        /// ID linh hoạt: Có thể là BranchProductId (nếu sản phẩm đã liên kết chi nhánh)
        /// hoặc ProductId gốc (nếu sản phẩm chưa từng có ở chi nhánh hiện tại).
        /// </summary>
        public Guid Id { get; set; }

        public decimal Quantity { get; set; }

        /// <summary>
        /// Giá vốn / Giá nhập hàng đợt này (Phục vụ cập nhật lại CostPrice cho hệ thống chi nhánh).
        /// </summary>
        public decimal CostPrice { get; set; }

        public string? Note { get; set; }
    }

    public class InwardInventoryCreateDto
    {
        public InwardInventoryType InwardType { get; set; }
        public Guid? SupplierId { get; set; }
        public string Reason { get; set; } = null!;
        public string? Note { get; set; }
        public List<InwardInventoryItemDto> Items { get; set; } = new();
    }

    public class InwardInventoryUpdateDto
    {
        public string? Reason { get; set; }
        public string? Note { get; set; }
        public string? Status { get; set; }
    }

    public class InwardInventoryItemResponseDto
    {
        public Guid TicketItemId { get; set; }
        public Guid BranchProductId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductCode { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public decimal SystemQuantity { get; set; }
        public decimal Quantity { get; set; }
        public decimal ActualQuantity { get; set; }
    }

    public class InwardInventoryResponseDto
    {
        public Guid StockTicketId { get; set; }
        public string TicketCode { get; set; } = null!;
        public string TicketType { get; set; } = null!;
        public string Status { get; set; } = null!;
        public Guid BranchId { get; set; }
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public string? Reason { get; set; }
        public string? Note { get; set; }
        public Guid? SupplierId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<InwardInventoryItemResponseDto> Items { get; set; } = new();
    }

    public class BulkToggleStatusRequest
    {
        public List<Guid> Ids { get; set; } = new();
        public bool IsActive { get; set; }
    }

    public class BulkToggleStatusResponse
    {
        public int UpdatedCount { get; set; }
        public List<Guid> UpdatedIds { get; set; } = new();
        public List<Guid> NotFoundIds { get; set; } = new();
        public string NewStatus { get; set; } = null!;
    }
    #endregion
}