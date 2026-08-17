using System;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.InventoryDTOs;

/// <summary>
/// DTO trả về thông tin tồn kho 1 dòng (1 BranchProduct hoặc gộp theo Product).
/// Các field khớp với contract FE của /api/Stock.
/// </summary>
public class StockResponseDto
{
    public Guid? BranchProductId { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }

    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string? Barcode { get; set; }
    public string? Unit { get; set; }

    /// <summary>Tên viết tắt = ProductCode.</summary>
    public string? Sku => ProductCode;

    /// <summary>Tồn kho thực tế (ActualStock) — field chính FE đọc.</summary>
    public decimal Stock { get; set; }
    public decimal ActualStock { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal ReservedStock { get; set; }
    public decimal WarrantyQuantity { get; set; }
    public decimal SellableQuantity => Math.Max(0, AvailableStock - WarrantyQuantity);

    public decimal MinimumStock { get; set; }
    public decimal MaximumStock { get; set; }

    public decimal CostPrice { get; set; }
    public decimal SellPrice { get; set; }

    /// <summary>IN_STOCK / LOW_STOCK / OUT_OF_STOCK</summary>
    public string StockStatus { get; set; } = "";

    public string? ShelfLocation { get; set; }
}

/// <summary>
/// Body gửi lên để điều chỉnh tồn kho (PUT /api/Stock/{productId}).
/// NewStock là giá trị tồn kho mới tuyệt đối cho BranchProduct của branch hiện tại.
/// </summary>
public class UpdateStockDto
{
    /// <summary>Tồn kho thực tế mới (ActualStock) sau khi điều chỉnh. Phải &gt;= 0.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Tồn kho mới phải lớn hơn hoặc bằng 0.")]
    public decimal NewStock { get; set; }

    public string? Note { get; set; }

    /// <summary>Lý do điều chỉnh (ghi vào inventory_ledger qua DocCode nếu có / note).</summary>
    public string? Reason { get; set; }
}