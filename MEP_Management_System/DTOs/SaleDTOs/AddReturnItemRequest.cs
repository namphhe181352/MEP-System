using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class AddReturnItemRequest
{
    [Required(ErrorMessage = "ProductId là bắt buộc.")]
    public Guid ProductId { get; set; }

    [Required(ErrorMessage = "Số lượng là bắt buộc.")]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0.")]
    public decimal Quantity { get; set; }

    [Required(ErrorMessage = "Lý do là bắt buộc.")]
    [RegularExpression(@"^(DEFECTIVE|WRONG_ITEM|NOT_SATISFIED|OTHER)$",
        ErrorMessage = "Lý do không hợp lệ. Các giá trị hợp lệ: DEFECTIVE, WRONG_ITEM, NOT_SATISFIED, OTHER.")]
    public string Reason { get; set; } = "DEFECTIVE";
}
