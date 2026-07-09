using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

/// <summary>
/// Nhóm chi phí (danh mục quản lý bởi Owner).
/// Ví dụ: Điện nước, Lương, Thuê mặt bằng, Vận chuyển...
/// </summary>
public partial class ExpenseCategory
{
    public Guid CategoryId { get; set; }

    /// <summary>Tên nhóm chi phí (unique, not null).</summary>
    public string CategoryName { get; set; } = null!;

    /// <summary>1=Active, 0=Inactive.</summary>
    public short IsActive { get; set; } = 1;

    /// <summary>Người tạo nhóm chi phí.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Thời gian tạo.</summary>
    public DateTime? CreatedAt { get; set; }

    // ======= Navigation =======
    public virtual User? CreatedByUser { get; set; }

    public virtual ICollection<ExpenseVoucher> ExpenseVouchers { get; set; } = new List<ExpenseVoucher>();
}
