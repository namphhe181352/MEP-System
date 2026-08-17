using System;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Services.SaleServices;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/sales/invoice-templates")]
public class InvoicePrintTemplateController : ControllerBase
{
    private readonly IInvoicePrintTemplateService _service;
    private readonly IPrintingService _printingService;

    public InvoicePrintTemplateController(IInvoicePrintTemplateService service, IPrintingService printingService)
    {
        _service = service;
        _printingService = printingService;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found"));

    [HttpGet]
    public async Task<ActionResult> GetTemplate()
    {
        var branchId = GetBranchId();
        var template = await _service.ResolveTemplateAsync(branchId);
        return Ok(template ?? InvoiceDefault());
    }

    private static InvoicePrintTemplate InvoiceDefault() => new()
    {
        HeaderText = "",
        FooterText = "",
        ShowLogo = false,
        LogoUrl = "",
        BranchName = "",
        BranchAddress = "",
        Phone = "",
        TaxCode = "",
        PaperSize = "K80",
        FontSize = 12,
        FontFamily = "monospace",
        ShowCustomerInfo = true,
        ShowCashierName = true,
        ShowBranchInfo = true,
        ShowPaymentMethod = true,
        ThankYouMessage = "Cảm ơn quý khách!",
        CustomFieldsJson = "{}",
        IsActive = true
    };

    [HttpPut]
    public async Task<ActionResult> UpsertTemplate([FromBody] UpsertInvoicePrintTemplateRequest req)
    {
        await _service.UpsertTemplateAsync(GetBranchId(), req);
        return Ok(new { message = "Template saved" });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteTemplate(Guid id)
    {
        await _service.DeleteTemplateAsync(id);
        return Ok(new { message = "Template deleted" });
    }

    [HttpPost("preview")]
    public async Task<ActionResult> Preview([FromQuery] Guid invoiceId)
    {
        var receipt = await _printingService.GetLatestReceiptAsync(invoiceId);
        if (receipt == null)
            return NotFound(new { message = "Receipt not found for this invoice" });
        return Content(receipt.HtmlContent ?? "", "text/html");
    }
}
