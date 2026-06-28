namespace MEP_Management_System.DTOs.SaleDTOs;

public class AddReturnItemRequest
{
    /// <summary>ID sản phẩm (BranchProductId).</summary>
    public Guid BranchProductId { get; set; }

    /// <summary>Số lượng muốn trả (≤ số lượng đã mua, BR-80).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Lý do: DEFECTIVE | WRONG_ITEM | NOT_SATISFIED | OTHER.</summary>
    public string Reason { get; set; } = "DEFECTIVE";
}
