using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.SaleDTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MEP_Management_System.Services;

public class VietQrService
{
    private readonly HttpClient _httpClient;
    private readonly VietQrOptions _options;
    private readonly ILogger<VietQrService> _logger;

    public VietQrService(HttpClient httpClient, IOptions<VietQrOptions> options, ILogger<VietQrService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri(_options.ApiBase.EndsWith("/") ? _options.ApiBase : _options.ApiBase + "/");
        if (!string.IsNullOrEmpty(_options.ClientId))
        {
            _httpClient.DefaultRequestHeaders.Add("x-client-id", _options.ClientId);
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        }
    }

    public async Task<(string qrImageBase64, string transactionContent)> GenerateQrAsync(decimal amount, string transactionContent)
    {
        try
        {
            var requestBody = new
            {
                accountNo = _options.AccountNumber,
                accountName = _options.AccountName,
                acqId = GetBankBin(_options.BankCode), // Using BankCode directly as BIN or Mapping is needed. VietQR API accepts acqId as BIN. 
                amount = (int)amount,
                addInfo = transactionContent,
                format = "text",
                template = _options.TemplateId
            };

            var response = await _httpClient.PostAsJsonAsync("generate", requestBody);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<VietQrResponse>();
            
            if (result?.Code == "00" && result.Data != null)
            {
                // result.Data.QrDataURL contains "data:image/png;base64,iVBORw..."
                return (result.Data.QrDataURL, transactionContent);
            }
            
            _logger.LogError("VietQR generation failed. Code: {Code}, Desc: {Desc}", result?.Code, result?.Desc);
            throw new Exception("VietQR generation failed: " + result?.Desc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating VietQR for amount {Amount}", amount);
            throw;
        }
    }

    // A simple mapper if BankCode is used. In production, we use actual BINs (like VCB = 970436)
    // For VietQR, acqId can be the BIN (6 digits) or the short name (VCB, MB). The API supports short names.
    private string GetBankBin(string bankCode)
    {
        return bankCode; 
    }
}

public class VietQrResponse
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public VietQrData? Data { get; set; }
}

public class VietQrData
{
    [JsonPropertyName("qrCode")]
    public string QrCode { get; set; } = string.Empty;

    [JsonPropertyName("qrDataURL")]
    public string QrDataURL { get; set; } = string.Empty;
}
