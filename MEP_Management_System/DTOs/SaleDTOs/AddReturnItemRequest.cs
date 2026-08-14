using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class AddReturnItemRequest
{
    [Required(ErrorMessage = "ProductId là bắt buộc.")]
    public Guid ProductId { get; set; }

    /// <summary>ID dòng hóa đơn gốc (InvoiceItem) đang trả — để pin đúng đơn vị/quy đổi
    /// khi cùng 1 SP có nhiều dòng khác đơn vị trong 1 hóa đơn. Có thể bỏ qua.</summary>
    public Guid? InvoiceItemId { get; set; }

    [Required(ErrorMessage = "Số lượng là bắt buộc.")]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0.")]
    public decimal Quantity { get; set; }

    /// <summary>Hệ số quy đổi từ ĐVT bán về ĐVT cơ bản (mặc định 1).</summary>
    public decimal ConversionRate { get; set; } = 1;

    [Required(ErrorMessage = "Lý do là bắt buộc.")]
    [RegularExpression(@"^(DEFECTIVE|WRONG_ITEM|NOT_SATISFIED|OTHER)$",
        ErrorMessage = "Lý do không hợp lệ. Các giá trị hợp lệ: DEFECTIVE, WRONG_ITEM, NOT_SATISFIED, OTHER.")]
    public string Reason { get; set; } = "DEFECTIVE";
}
