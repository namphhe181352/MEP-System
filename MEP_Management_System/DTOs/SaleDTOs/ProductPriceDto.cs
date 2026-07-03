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
}
