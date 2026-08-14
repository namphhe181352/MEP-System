using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class ReturnOrder
{
    public Guid ReturnOrderId { get; set; }

    /// <summary>Mã phiếu trả hàng (VD: RET-2026-0001), UNIQUE.</summary>
    public string? ReturnCode { get; set; }

    public Guid OrderId { get; set; }

    public Guid UserId { get; set; }

    public decimal RefundAmount { get; set; }

    /// <summary>Phương thức hoàn tiền: CASH / TRANSFER / EXCHANGE.</summary>
    public string? RefundMethod { get; set; }

    /// <summary>Loại đổi trả: EXCHANGE (đổi hàng) / REFUND (trả hàng).</summary>
    public string? ReturnType { get; set; }

    /// <summary>Trạng thái: PENDING / COMPLETED / CANCELLED.</summary>
    public string? Status { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }

    /// <summary>Delta tiền chênh lệch khi đổi hàng (đã có dấu, NULL cho bảo hành thuần):
    /// + = khách trả thêm, - = hoàn lại khách, 0 = đổi ngang giá. Chỉ áp dụng ReturnType=EXCHANGE (đổi lệch giá).</summary>
    public decimal? DeltaAmount { get; set; }

    /// <summary>Số tiền khách phải TRẢ THÊM (chỉ khi DeltaAmount > 0). = Max(0, DeltaAmount).</summary>
    public decimal PayAmount { get; set; }

    /// <summary>Số tiền HOÀN LẠI khách (chỉ khi DeltaAmount &lt; 0). = Max(0, -DeltaAmount).</summary>
    public decimal RefundAmountCustomer { get; set; }

    /// <summary>Phương thức thanh toán chênh lệch: CASH / TRANSFER. Áp dụng cả 2 chiều khi Delta≠0.</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>FK (soft) tới payments — link Payment chênh lệch của exchange-diff delta>0
    /// (Cash đã Confirm / Transfer chờ Confirm). NULL cho ngang giá/refund/warranty.</summary>
    public Guid? PaymentId { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<ReturnItem> ReturnItems { get; set; } = new List<ReturnItem>();

    /// <summary>Danh sách hàng mới (SP B) xuất cho khách khi đổi hàng chênh lệch.</summary>
    public virtual ICollection<ExchangeItem> ExchangeItems { get; set; } = new List<ExchangeItem>();

    public virtual User User { get; set; } = null!;
}
