using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace MEP_Management_System.Models
{
 public partial class SupplierPayment
 {
 public Guid PaymentId { get; set; }
 public Guid SupplierId { get; set; }
 public Guid? PurchaseOrderId { get; set; }
 public decimal Amount { get; set; }
 public DateTime PaymentDate { get; set; }
 public string PaymentMethod { get; set; } = "";
 public string? ReferenceCode { get; set; }
 public string? Note { get; set; }

 // 1. Giữ nguyên cột gốc có sẵn trong Database của bạn
 [Column("RecordedBy")]
 public Guid RecordedBy { get; set; }

 public DateTime CreatedAt { get; set; }
 public short IsDeleted { get; set; } = 0;

 /// <summary>Owner (user_id) sở hữu phiếu chi này — dùng để tenant-isolation.</summary>
 public Guid? CreatedBy { get; set; }

 public virtual Supplier Supplier { get; set; } = null!;
 public virtual PurchaseOrder? PurchaseOrder { get; set; }

 // 2. Ép Entity Framework map thuộc tính Navigation này trỏ thẳng vào cột RecordedBy ở trên
 [ForeignKey("RecordedBy")]
 public virtual User RecordedByUser { get; set; } = null!;

 public virtual User? CreatedByUser { get; set; }
 }
}