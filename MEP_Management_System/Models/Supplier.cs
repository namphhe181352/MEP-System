using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class Supplier
{
    public Guid SupplierId { get; set; }

    public string SupplierName { get; set; } = null!;

    public string? ContactPhone { get; set; }

    /// <summary>Email liên hệ NCC.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Người liên hệ chính (tên nhân viên sales/bộ phận kinh doanh).</summary>
    public string? ContactPerson { get; set; }

    /// <summary>Mã số thuế.</summary>
    public string? TaxCode { get; set; }

    /// <summary>Nhóm NCC: "VLTho" (vật liệu thô), "PhuKien" (phụ kiện cơ khí), "VTHan" (vật tư hàn)...</summary>
    public string? GroupName { get; set; }

    /// <summary>Số tài khoản ngân hàng.</summary>
    public string? BankAccount { get; set; }

    /// <summary>Tên ngân hàng.</summary>
    public string? BankName { get; set; }

    /// <summary>Website NCC.</summary>
    public string? Website { get; set; }

    /// <summary>Ghi chú nội bộ.</summary>
    public string? Notes { get; set; }

    /// <summary>Địa chỉ kho/VP NCC.</summary>
    public string? Address { get; set; }

    /// <summary>Trạng thái NCC: 1=Active, 0=Inactive. Mặc định 1.</summary>
    public short? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    // ======= ĐÁNH GIÁ (cache từ bảng supplier_ratings) =======
    /// <summary>Điểm trung bình 1-5, snapshot từ bảng SupplierRatings. Null nếu chưa có đánh giá.</summary>
    public decimal? AverageRating { get; set; }

    /// <summary>Số lượt đã đánh giá.</summary>
    public int? RatingCount { get; set; }

    /// <summary>Ngày đánh giá gần nhất.</summary>
    public DateTime? LastRatedAt { get; set; }

    // ======= Navigation =======
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<SupplierRating> SupplierRatings { get; set; } = new List<SupplierRating>();

 /// <summary>Owner (user_id) đã tạo NCC này — dùng để tenant-isolation.</summary>
 public Guid? CreatedBy { get; set; }

 public virtual User? CreatedByUser { get; set; }
}
