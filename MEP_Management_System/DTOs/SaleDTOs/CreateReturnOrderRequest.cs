namespace MEP_Management_System.DTOs.SaleDTOs;

public class CreateReturnOrderRequest
{
    /// <summary>ID hóa đơn gốc cần đổi/trả (BR-81).</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>Phương thức hoàn tiền: CASH | TRANSFER | EXCHANGE.</summary>
    public string RefundMethod { get; set; } = "CASH";

    /// <summary>Ghi chú (tùy chọn).</summary>
    public string? Note { get; set; }
}
