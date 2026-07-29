namespace MEP_Management_System.DTOs;

public class TopProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public decimal Revenue { get; set; }
    public int QuantitySold { get; set; }
    public decimal Profit { get; set; }
}
