using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.InventoryDTOs;

/// <summary>
/// Payload tạo / cập nhật phiếu xuất kho nội bộ.
/// Field khớp <c>buildIssuePayload</c> ở FE (goodsIssueUtils.js).
/// </summary>
public class GoodsIssueCreateDto
{
    public string? IssueNumber { get; set; }
    public string? IssueType { get; set; }
    /// <summary>Định danh khách hàng do FE gửi (mã / guid string).</summary>
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public DateTime? Date { get; set; }
    public string? CreatedBy { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal? TotalPayment { get; set; }
    public List<GoodsIssueItemCreateDto> Lines { get; set; } = new();
}

public class GoodsIssueItemCreateDto
{
    public int LineNumber { get; set; }
    public string? ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public string? Unit { get; set; }
    public string? LotNumber { get; set; }
    public string? ExpiryDate { get; set; }
    public string? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string? Location { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// Response phiếu xuất kho. Field được chọn để khớp <c>normalizeIssueRow</c>
/// và form popup Sửa ở FE.
/// </summary>
public class GoodsIssueResponseDto
{
    /// <summary>Guid dạng string (để FE dùng cho PUT/DELETE theo route {id:guid}).</summary>
    public string Id { get; set; } = "";
    public Guid GoodsIssueId { get; set; }
    public string IssueNumber { get; set; } = "";
    public string IssueType { get; set; } = "";
    public string? CustomerId { get; set; }
    public string? Customer { get; set; }
    public string? CustomerName { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public DateTime? Date { get; set; }
    public string? CreatedBy { get; set; }
    public string? SyncStatus { get; set; }
    public string? SyncNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? TotalPayment { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Status { get; set; }
    public Guid BranchId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<GoodsIssueItemResponseDto> Lines { get; set; } = new();
    public List<GoodsIssueAttachmentResponseDto> Attachments { get; set; } = new();
}

public class GoodsIssueItemResponseDto
{
    public Guid GoodsIssueItemId { get; set; }
    public int LineNumber { get; set; }
    public string? ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public string? Unit { get; set; }
    public string? LotNumber { get; set; }
    public string? ExpiryDate { get; set; }
    public string? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string? Location { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
}

public class GoodsIssueAttachmentResponseDto
{
    public Guid GoodsIssueAttachmentId { get; set; }
    public string FileName { get; set; } = "";
    public string? ContentType { get; set; }
    public long? Size { get; set; }
    public string MediaUrl { get; set; } = "";
    public DateTime? UploadedAt { get; set; }
}

/// <summary>Sản phẩm trả về khi tra cứu nhanh để chọn vào dòng xuất kho.</summary>
public class GoodsIssueSearchProductDto
{
    public string Id { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public decimal Stock { get; set; }
    public string? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public bool HasLotControl { get; set; }
    public bool HasExpiryControl { get; set; }
}

/// <summary>Khách hàng chọn nhanh khi tạo phiếu xuất.</summary>
public class GoodsIssueCustomerDto
{
    public string Id { get; set; } = "";
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

/// <summary>Kho/branch trả về ở GET /goods-issue/warehouses.</summary>
public class GoodsIssueWarehouseDto
{
    public string Id { get; set; } = "";
    public string? Code { get; set; }
    public string? Name { get; set; }
}