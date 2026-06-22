namespace MEP_Management_System.DTOs.SaleDTOs;

public class ScanItemRequest
{
    public string Barcode { get; set; } = null!;
    public decimal Quantity { get; set; } = 1;
}
