using MEP_Management_System.Exceptions;
using System;
using System.Text;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/invoices/{invoiceId:guid}")]
[HasPermission(PermissionConstants.PRINT_VIEW)]
[Tags("Quản lý bán hàng (Sale)")]
public class PrintingController : ControllerBase
{
    private readonly IPrintingService _printingService;

    public PrintingController(IPrintingService printingService)
    {
        _printingService = printingService;
    }

    private Guid GetBranchId() => Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found"));

    [HttpPost("print")]
    public async Task<ActionResult<ReceiptDto>> PrintInvoice(Guid invoiceId)
    {
        try
        {
            var dto = await _printingService.PrintInvoiceAsync(invoiceId, GetBranchId());
            return Ok(dto);
        }
        catch (BusinessException ex) { return NotFound(ApiErrorResponse.Create("MSG-01", ex.Message)); }
    }

    [HttpGet("print.pdf")]
    public async Task<ActionResult> DownloadPdf(Guid invoiceId)
    {
        var receipt = await _printingService.GetLatestReceiptAsync(invoiceId);
        if (receipt == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Receipt not found"));
        if (receipt.Invoice.Order.BranchId != GetBranchId()) return Forbid();
        var bytes = Encoding.UTF8.GetBytes(receipt.HtmlContent ?? "");
        return File(bytes, "text/html", $"{receipt.ReceiptCode}.html");
    }
}
