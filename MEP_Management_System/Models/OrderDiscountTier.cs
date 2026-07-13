using System;

namespace MEP_Management_System.Models;

public partial class OrderDiscountTier
{
    public Guid OrderDiscountTierId { get; set; }

    /// <summary>ID chi nhánh (null = global discount).</summary>
    public Guid? BranchId { get; set; }

    /// <summary>Ngưỡng giá trị đơn hàng tối thiểu.</summary>
    public decimal MinOrderValue { get; set; }

    /// <summary>% chiết khấu áp dụng cho ngưỡng này.</summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>Trạng thái hoạt động.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Branch? Branch { get; set; }
}
