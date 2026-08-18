using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.SaleDTOs;

// ============================================================
// Đổi hàng chênh lệch (Exchange with Price Difference)
// ============================================================

/// <summary>Hàng trả về (SP A) — dòng hóa đơn gốc khách muốn đổi.</summary>
public class ExchangeQuoteItem
{
    public Guid InvoiceItemId { get; set; }   // dòng hóa đơn gốc
    public decimal Quantity { get; set; }      // SL đổi (ĐVT bán của dòng đó)
    public string Reason { get; set; } = "DEFECTIVE"; // DEFECTIVE/WRONG_ITEM/NOT_SATISFIED/OTHER
}

/// <summary>Hàng mới (SP B) — xuất cho khách.</summary>
public class ExchangeNewItem
{
    public Guid BranchProductId { get; set; }
    public decimal Quantity { get; set; }       // SL theo ĐVT bán
    public decimal? ConversionRate { get; set; } // null → tự lấy ProductConversionUnit
    /// <summary>Đơn giá theo ĐVT khách chọn (theo Thùng/Cái...). Client gửi như luồng bán hàng.
    /// Nếu null/0 → backend quy đổi từ giá base: bp.SellPrice * ConversionRate.</summary>
    public decimal? UnitPrice { get; set; }
}

/// <summary>Body dry-run: tính Delta + check tồn SP B, KHÔNG ghi DB.</summary>
public class ExchangeQuoteRequest
{
    public Guid InvoiceId { get; set; }
    public List<ExchangeQuoteItem> ReturnItems { get; set; } = new();
    public List<ExchangeNewItem> NewItems { get; set; } = new();
}

public class StockAvailabilityDto
{
    public Guid BranchProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public decimal RequestedBase { get; set; }
    public decimal AvailableBase { get; set; }   // AvailableStock - WarrantyQuantity - ReservedStock
    public bool Sufficient { get; set; }
}

/// <summary>Kết quả dry-run cho UI hiển thị Delta trước khi chốt.</summary>
public class ExchangeQuoteResponse
{
    public bool Eligible { get; set; }
    public string? RestrictionReason { get; set; }      // lý do chặn nếu any
    public string? RuleBreak { get; set; }              // mã rule vi phạm (vd "EXCHANGE_DAYS", "STOCK", "DUPLICATE")
    public decimal OldValue { get; set; }                // Σ giá trị SP A
    public decimal NewValue { get; set; }                // Σ giá trị SP B
    public decimal DeltaAmount { get; set; }              // NewValue - OldValue (có dấu)
    public string DeltaLabel { get; set; } = "";          // "Đổi ngang giá" / "Thu thêm" / "Hoàn lại"
    public List<StockAvailabilityDto> StockCheck { get; set; } = new();
}

/// <summary>Body thực thi đổi hàng chênh lệch.</summary>
public class CreateExchangeRequest
{
    public Guid InvoiceId { get; set; }
    public string PaymentMethod { get; set; } = "CASH"; // CASH/TRANSFER — áp dụng khi Delta≠0
    public string? Note { get; set; }
    public List<ExchangeQuoteItem> ReturnItems { get; set; } = new();
    public List<ExchangeNewItem> NewItems { get; set; } = new();
}

/// <summary>DTO dòng hàng mới (SP B).</summary>
public class ExchangeItemDto
{
    public Guid ExchangeItemId { get; set; }
    public Guid BranchProductId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public string? Unit { get; set; }
    public string? BaseUnit { get; set; }
    public decimal Quantity { get; set; }
    public decimal ConversionRate { get; set; } = 1;
    public decimal BaseQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
