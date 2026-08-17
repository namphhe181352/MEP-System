using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.InventoryDTOs
{
    /// <summary>
    /// Request body cho POST /api/StockImport (phiếu nhập kho legacy).
    /// Shape tương thích FE: createImport(payload). Delegate sang InwardInventory.
    /// </summary>
    public class StockImportCreateDto
    {
        /// <summary>Loại nhập: 1 = Purchase, 2 = CustomerReturn, 3 = BalanceAdjust.</summary>
        public InwardInventoryType InwardType { get; set; }

        /// <summary>Bắt buộc khi InwardType = Purchase.</summary>
        public Guid? SupplierId { get; set; }

        public string Reason { get; set; } = null!;
        public string? Note { get; set; }

        public List<StockImportItemDto> Items { get; set; } = new();
    }

    /// <summary>
    /// Một dòng sản phẩm trong phiếu nhập legacy.
    /// Id có thể là BranchProductId / ProductId (Guid) hoặc ProductCode (string).
    /// Nếu là sản phẩm mới: truyền productCode + productName (không cần Id).
    /// </summary>
    public class StockImportItemDto
    {
        public string? Id { get; set; }
        public string? ProductCode { get; set; }
        public string? ProductName { get; set; }
        public string? Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostPrice { get; set; }
        public string? Note { get; set; }
    }

    public class StockImportItemResponseDto
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
        public string? UnitName { get; set; }
    }

    /// <summary>
    /// Response phiếu nhập kho legacy — đồng bộ field với InwardInventoryResponseDto
    /// để FE (list/detail) đọc: ticketCode, status, items[].productName/quantity,
    /// createdAt, reason/note, cancelReason, ...
    /// </summary>
    public class StockImportResponseDto
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
        public decimal CostPrice { get; set; }
        public Guid? SupplierId { get; set; }

        public DateTime? CreatedAt { get; set; }

        // === Audit trail ===
        public DateTime? CompletedAt { get; set; }
        public Guid? ConfirmedBy { get; set; }
        public DateTime? CancelledAt { get; set; }
        public Guid? CancelledBy { get; set; }
        public string? CancelReason { get; set; }

        public List<StockImportItemResponseDto> Items { get; set; } = new();
    }
}