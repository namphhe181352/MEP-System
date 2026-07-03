namespace MEP_Management_System.DTOs.SaleDTOs;

public class VietQrOptions
{
    public string ApiBase { get; set; } = "https://api.vietqr.io/v2";
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    
    // Default bank info from config
    public string BankCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    
    public string WebhookSecret { get; set; } = string.Empty;
}
