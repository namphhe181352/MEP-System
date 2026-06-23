using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class ProductStockDto
{
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public decimal InStockQty { get; set; }
    public decimal InTransitQty { get; set; }
    public List<BranchStockDto> ByBranch { get; set; } = new();

    public class BranchStockDto
    {
        public Guid BranchId { get; set; }
        public string? BranchName { get; set; }
        public decimal AvailableStock { get; set; }
        public decimal InTransitStock { get; set; }
    }
}
