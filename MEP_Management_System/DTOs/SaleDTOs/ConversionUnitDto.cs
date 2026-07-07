namespace MEP_Management_System.DTOs.SaleDTOs;

public class ConversionUnitDto
{
    public Guid ConversionId { get; set; }
    public string UnitName { get; set; } = null!;
    public decimal ConvertValue { get; set; }
    public decimal Price { get; set; }
    public bool DirectSale { get; set; }
}
