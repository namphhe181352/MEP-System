using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.InventoryDTOs
{
    #region Product DTOs
    public class ProductUpsertDto
    {
        public Guid? ProductId { get; set; }

        public Guid? BranchProductId { get; set; }

        public string? Id { get; set; }
        public string ProductCode { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public string? Barcode { get; set; }
        public string Unit { get; set; } = null!;
        public Guid? BrandTaxonomyId { get; set; }
        public Guid? CategoryTaxonomyId { get; set; }
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
        public decimal WarrantyQuantity { get; set; }
        public decimal SellableQuantity => Math.Max(0, AvailableStock - WarrantyQuantity);
        public decimal? MinimumStock { get; set; }
        public decimal? MaximumStock { get; set; }
        public string? ShelfLocation { get; set; }
        public List<string> ShelfLocations { get; set; } = new();
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public decimal? Width { get; set; }
        public decimal? Length { get; set; }
        public decimal? Height { get; set; }
        public string? SizeUnit { get; set; }
        public string? Specification { get; set; }
        public string? SpecificationDetail { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> Images { get; set; } = new();
        public bool DirectSale { get; set; }
        public bool SalesChannelLinked { get; set; }
        public bool IsActive { get; set; }
        public List<ProductAttributeDto> Attributes { get; set; } = new();
        public List<ProductConversionUnitDto> ConversionUnits { get; set; } = new();
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

    public class ProductConversionUnitDto
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

        public List<ProductMediaDto> Medias { get; set; } = new();
    }

    public class UpdateProductDto
    {
        public string ProductCode { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public string? Barcode { get; set; }
        public string Unit { get; set; } = null!;
        public string? Specification { get; set; }
        public string? ItemType { get; set; }
        public string? BrandName { get; set; }
        public string? CategoryName { get; set; }
        public Guid? SupplierId { get; set; }

        public decimal CostPrice { get; set; }
        public decimal SalePrice { get; set; }
        public decimal ActualStock { get; set; }
        public decimal ReservedStock { get; set; }
        public decimal AvailableStock { get; set; }
        public decimal WarrantyQuantity { get; set; }
        public decimal SellableQuantity => Math.Max(0, AvailableStock - WarrantyQuantity);
        public decimal? MinimumStock { get; set; }
        public decimal? MaximumStock { get; set; }
        public string? ShelfLocation { get; set; }
        public List<string> ShelfLocations { get; set; } = new();
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public decimal? Width { get; set; }
        public decimal? Length { get; set; }
        public decimal? Height { get; set; }
        public string? SizeUnit { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> Images { get; set; } = new();
        public bool DirectSale { get; set; }
        public List<ProductAttributeDto> Attributes { get; set; } = new();
        public List<ProductConversionUnitDto> ConversionUnits { get; set; } = new();
    }

    public class ProductParameters
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SearchTerm { get; set; }
        public string? Sort { get; set; }
        public string? SortOrder { get; set; }
        public string? Order { get; set; } = "asc";
        public string? CategoryName { get; set; }
        public string? BrandName { get; set; }
        public string? Status { get; set; }
        public Guid? SupplierId { get; set; }

    }
    #endregion Product Search DTOs
    /// <summary>
    /// Kảt quả trả về cho autocomplete tìm sản phẩm trong form nhập hàng.
    /// </summary>
    public class ProductSearchResultDto
    {
        public Guid ProductId { get; set; }
        public Guid BranchProductId { get; set; }
        public string ProductCode { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public string? BrandName { get; set; }
        public string? CategoryName { get; set; }
        public string? Barcode { get; set; }
        public string? Unit { get; set; }
        public string? ImageUrl { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal WarrantyQuantity { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SalePrice { get; set; }
        public int WarrantyPeriod { get; set; }
        public string WarrantyUnit { get; set; } = "MONTH";
    }

    /// <summary>
    /// Gợi ý thông tin khi user muốn tạo sản phẩm mới ngay trong form nhập hàng.
    /// </summary>
    public class NewProductSuggestionDto
    {
        public string SuggestedCode { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public string DefaultUnit { get; set; } = "Cái";
        public List<string> BrandSuggestions { get; set; } = new();
        public List<string> CategorySuggestions { get; set; } = new();
    }

    public class TaxonomyItemDto
    {
        public string Name { get; set; } = null!;
        public int ProductCount { get; set; }
    }

    public class CreateTaxonomyDto
    {
        public string Name { get; set; } = null!;
    }

    public class RenameTaxonomyDto
    {
        public string OldName { get; set; } = null!;
        public string NewName { get; set; } = null!;
    }

    public class CreateAttributeTypeDto
    {
        [Required(ErrorMessage = "Tên loại thuộc tính là bắt buộc")]
        [MaxLength(100, ErrorMessage = "Tên không quá 100 ký tự")]
        public string TypeName { get; set; } = null!;

        [MaxLength(50, ErrorMessage = "Mã không quá 50 ký tự")]
        public string? TypeCode { get; set; }
    }

    public class UpdateAttributeTypeDto
    {
        [MaxLength(100, ErrorMessage = "Tên không quá 100 ký tự")]
        public string? TypeName { get; set; }

        [MaxLength(50, ErrorMessage = "Mã không quá 50 ký tự")]
        public string? TypeCode { get; set; }
    }



    #region Quick Inward DTOs
    /// <summary>
    /// Request tạo phiếu nhập kho "1 bước" giống KiotViet:
    /// Hỗ trợ cả sản phẩm đã có (chọn từ dropdown) lẩn tạo mới inline.
    /// </summary>
    public class QuickInwardCreateDto
    {

        public Guid? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? Reason { get; set; }
        public string? Note { get; set; }

        /// <summary>Thời hạn bảo hành của lô nhập này (số lượng). 0 = không BH.</summary>
        public int WarrantyPeriod { get; set; }

        /// <summary>Đơn vị bảo hành: DAY, MONTH, YEAR.</summary>
        public string WarrantyUnit { get; set; } = "MONTH";

        /// <summary>Nếu null hoặc rỗng → hệ thống tự generate mã.</summary>
        public string? TicketCode { get; set; }

        public InwardInventoryType InwardType { get; set; } = InwardInventoryType.Purchase;

        public List<QuickInwardItemDto> Items { get; set; } = new();
    }

    public class QuickInwardItemDto
    {
        /// <summary>Sản phẩm đã có sẵn trong hệ thống (chọn từ dropdown).</summary>
        public Guid? ProductId { get; set; }

        /// <summary>Thông tin sản phẩm mới (nếeu không tìm thấy trong dropdown).</summary>
        public NewProductInQuickInwardDto? NewProduct { get; set; }

        public decimal Quantity { get; set; }
        public decimal CostPrice { get; set; }
        public decimal? SalePrice { get; set; }
        public string? Note { get; set; }
    }

    public class NewProductInQuickInwardDto
    {
        public string ProductCode { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public string Unit { get; set; } = "Cái";
        public string? BrandName { get; set; }
        public string? CategoryName { get; set; }
        public string? Barcode { get; set; }
        public decimal SalePrice { get; set; }
        public Guid? SupplierId { get; set; }

    }

    /// <summary>Response trả về sau khi tạo phiếu nhập thành công.</summary>
    public class QuickInwardResponseDto
    {
        public Guid TicketId { get; set; }
        public string TicketCode { get; set; } = null!;
        public string TicketType { get; set; } = null!;
        public Guid BranchId { get; set; }
        public int TotalItems { get; set; }
        public List<QuickInwardItemResultDto> Results { get; set; } = new();
    }

    public class QuickInwardItemResultDto
    {
        public Guid ProductId { get; set; }
        public string ProductCode { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public bool WasNewProduct { get; set; }
        public decimal Quantity { get; set; }
        public decimal NewStock { get; set; }
    }
    #endregion

    #region Outward Inventory DTOs
    public enum OutwardInventoryType
    {
        ReturnToSupplier = 1,
        DamagedHaoHut = 2,
        InternalUse = 3,
        SupplierReturn = 4,
        Sale = 5
    }

    public class OutwardInventoryItemDto
    {
        /// <summary>Flexible ID: can be BranchProductId, ProductId, or ProductCode.</summary>
        public string? Id { get; set; }
        public Guid BranchProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? CostPrice { get; set; }
        /// <summary>FK tới dòng hàng của phiếu nhập gốc (dùng khi xuất trả NCC).</summary>
        public Guid? InwardTicketItemId { get; set; }
    }

    public class OutwardInventoryCreateDto
    {
        public OutwardInventoryType OutwardType { get; set; }
        public Guid? SupplierId { get; set; }
        public string Reason { get; set; } = null!;
        public string? Note { get; set; }
        /// <summary>FK tới phiếu nhập gốc (dùng khi xuất trả NCC).</summary>
        public Guid? InwardTicketId { get; set; }
        public List<OutwardInventoryItemDto> Items { get; set; } = new();
    }

    public class OutwardInventoryUpdateDto
    {
        public string? Reason { get; set; }
        public string? Note { get; set; }
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
        public decimal? UnitPrice { get; set; }
        public decimal? CostPrice { get; set; }
        public string? UnitName { get; set; }
        public string? SelectedUnit { get; set; }
        public string? BaseUnit { get; set; }
        public decimal? ConvertValue { get; set; }
        public decimal? SaleQuantity { get; set; }
        public decimal? SellPrice { get; set; }
        public decimal? TotalPrice { get; set; }
        /// <summary>FK tới dòng hàng của phiếu nhập gốc.</summary>
        public Guid? InwardTicketItemId { get; set; }
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
        public Guid? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        /// <summary>FK tới phiếu nhập gốc (dùng khi xuất trả NCC).</summary>
        public Guid? InwardTicketId { get; set; }
        public string? Reason { get; set; }
        public string? Note { get; set; }
        public DateTime? CreatedAt { get; set; }

        // === Audit trail ===
        public DateTime? CompletedAt { get; set; }
        public Guid? ConfirmedBy { get; set; }
        public DateTime? CancelledAt { get; set; }
        public Guid? CancelledBy { get; set; }
        public string? CancelReason { get; set; }

        /// <summary>Chỉ true khi Status=PENDING VÀ user có quyền STOCK_OUTWARD_CONFIRM hoặc là Owner.</summary>
        public bool CanConfirm { get; set; }

        public decimal TotalAmount { get; set; }

        public List<OutwardInventoryItemResponseDto> Items { get; set; } = new();
    }

    /// <summary>Response rút gọn cho danh sách phiếu nhập của 1 NCC (dùng để chọn khi xuất trả).</summary>
    public class InwardTicketForReturnDto
    {
        public Guid StockTicketId { get; set; }
        public string TicketCode { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public int ItemsCount { get; set; }
    }

    /// <summary>Response cho API lấy danh sách sản phẩm có thể trả của 1 phiếu nhập.</summary>
    public class ReturnableItemDto
    {
        public Guid InwardTicketItemId { get; set; }
        public Guid BranchProductId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductCode { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public string? UnitName { get; set; }
        public decimal OriginalQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        /// <summary>Số lượng còn lại có thể trả (đã trừ các lần trả trước).</summary>
        public decimal MaxReturnableQuantity { get; set; }
        /// <summary>Tồn kho thực tế hiện tại.</summary>
        public decimal ActualStock { get; set; }
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
        /// SupplierId cho sản phẩm đã tồn tại — gán tại phiếu nhập kho.
        /// </summary>
        public Guid? SupplierId { get; set; }

        /// <summary>
        /// ID linh hoạt: Có thể là BranchProductId, ProductId (Guid)
        /// hoặc ProductCode (string, ví dụ: SP-001) từ Frontend.
        /// Để trống nếu là sản phẩm mới (điền productCode + productName trực tiếp).
        /// </summary>
        public string? Id { get; set; }

        /// <summary>Thông tin sản phẩm mới nếu chưa có trong hệ thống (dạng nested).</summary>
        public NewProductInQuickInwardDto? NewProduct { get; set; }

        // --- Các field sản phẩm mới flat (FE gửi trực tiếp, không nested) ---
        /// <summary>Mã sản phẩm mới (nếu tạo inline).</summary>
        public string? ProductCode { get; set; }
        /// <summary>Tên sản phẩm mới (nếu tạo inline).</summary>
        public string? ProductName { get; set; }
        /// <summary>Đơn vị tính của sản phẩm mới.</summary>
        public string? Unit { get; set; }
        /// <summary>Thương hiệu (legacy).</summary>
        public string? BrandName { get; set; }
        /// <summary>Danh mục (legacy).</summary>
        public string? CategoryName { get; set; }
        /// <summary>Mã vạch.</summary>
        public string? Barcode { get; set; }
        /// <summary>Giá bán của sản phẩm mới.</summary>
        public decimal SalePrice { get; set; }

        public decimal Quantity { get; set; }

        /// <summary>
        /// Giá vốn / Giá nhập hàng đợt này (Phục vụ cập nhật lại CostPrice cho hệ thống chi nhánh).
        /// </summary>
        public decimal CostPrice { get; set; }

        public string? Note { get; set; }

        /// <summary>Thời hạn bảo hành của lô nhập này (số lượng). 0 = không BH.</summary>
        public int WarrantyPeriod { get; set; }

        /// <summary>Đơn vị bảo hành: DAY, MONTH, YEAR.</summary>
        public string WarrantyUnit { get; set; } = "MONTH";

        /// <summary>
        /// SupplierId cho sản phẩm đã tồn tại — gán tại phiếu nhập kho.
        /// </summary>

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

        public decimal? UnitPrice { get; set; }
        public decimal? CostPrice { get; set; }
        public string? UnitName { get; set; }

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

        public DateTime? CreatedAt { get; set; }

        // === Audit trail ===
        public DateTime? CompletedAt { get; set; }
        public Guid? ConfirmedBy { get; set; }
        public DateTime? CancelledAt { get; set; }
        public Guid? CancelledBy { get; set; }
        public string? CancelReason { get; set; }

        public List<InwardInventoryItemResponseDto> Items { get; set; } = new();
        public decimal CostPrice { get; set; }
        public Guid? SupplierId { get; set; }
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

    #region Confirm/Cancel DTOs
    /// <summary>
    /// Request body cho API Cancel phiếu nhập/xuất kho.
    /// </summary>
    public class CancelTicketDto
    {
        /// <summary>Lý do hủy phiếu (bắt buộc).</summary>
        public string CancelReason { get; set; } = null!;
    }

    /// <summary>
    /// Request body cho API Update Item trong phiếu PENDING.
    /// </summary>
    public class UpdateTicketItemDto
    {
        public Guid? ItemId { get; set; }
        public Guid BranchProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostPrice { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>
    /// Request body cho API Update phiếu (chỉ khi PENDING).
    /// </summary>
    public class UpdateTicketDto
    {
        public string? Reason { get; set; }
        public string? Note { get; set; }
        public List<UpdateTicketItemDto>? Items { get; set; }
    }

    /// <summary>
    /// DTO for bulk importing products from Excel
    /// </summary>
    public class ProductBulkImportDto
    {
        public List<ProductUpsertDto> Products { get; set; } = new();
        public bool AutoCreateMissingCategories { get; set; } = true;
        public bool AutoCreateMissingBrands { get; set; } = true;
    }
    #endregion
}