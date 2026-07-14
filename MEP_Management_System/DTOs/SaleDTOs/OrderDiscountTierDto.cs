using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class OrderDiscountTierDto
{
    public Guid OrderDiscountTierId { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public decimal MinOrderValue { get; set; }
    public decimal DiscountPercent { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateOrderDiscountTierDto
{
    /// <summary>ID chi nhánh (null = global discount).</summary>
    public Guid? BranchId { get; set; }

    /// <summary>Ngưỡng giá trị đơn hàng tối thiểu (phải > 0).</summary>
    public decimal MinOrderValue { get; set; }

    /// <summary>% chiết khấu (0.01 - 100).</summary>
    public decimal DiscountPercent { get; set; }
}

public class UpdateOrderDiscountTierDto : CreateOrderDiscountTierDto
{
    /// <summary>Trạng thái hoạt động.</summary>
    public bool IsActive { get; set; }
}
