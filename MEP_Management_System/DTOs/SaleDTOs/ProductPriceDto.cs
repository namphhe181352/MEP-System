using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class ProductPriceDto
{
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public string? Barcode { get; set; }
    public decimal RetailPrice { get; set; }
    public decimal? FloorPrice { get; set; }
    public string? Unit { get; set; }
    public decimal? AvailableStock { get; set; }
    public string? ImageUrl { get; set; }
    public string? ProductStatus { get; set; }
    /// <summary>Đơn vị cơ bản có được phép bán trực tiếp không.</summary>
    public bool DirectSale { get; set; }

    /// <summary>Các đơn vị quy đổi có thể bán (ví dụ: Cuộn, Hộp).</summary>
    public List<ConversionUnitDto>? ConversionUnits { get; set; }
}
