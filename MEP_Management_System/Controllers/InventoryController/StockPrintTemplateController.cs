using System;
using System.Threading.Tasks;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Services.InventoryService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.InventoryController;

[ApiController]
[Route("api/inventory/print-templates/stock-ticket")]
[Tags("Mẫu in phiếu kho")]
public class StockPrintTemplateController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly IStockPrintTemplateService _service;

    public StockPrintTemplateController(MEPDatabaseContext context, IStockPrintTemplateService service)
    {
        _context = context;
        _service = service;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found"));

    [HttpGet]
    public async Task<ActionResult> GetTemplate([FromQuery] string ticketType)
    {
        var branchId = GetBranchId();
        var template = await _service.ResolveTemplateAsync(branchId, ticketType ?? "STOCK_CHECK");
        return Ok(template ?? StockDefault(ticketType ?? "STOCK_CHECK"));
    }

    private static StockPrintTemplate StockDefault(string ticketType) => new()
    {
        TicketType = ticketType,
        HeaderText = "",
        FooterText = "",
        ShowLogo = false,
        LogoUrl = "",
        BranchName = "",
        BranchAddress = "",
        Phone = "",
        TaxCode = "",
        PaperSize = "A5",
        FontSize = 12,
        FontFamily = "monospace",
        ShowSupplier = true,
        ShowBranchInfo = true,
        ShowSignature = true,
        CustomFieldsJson = "{}",
        IsActive = true
    };

    [HttpPut]
    public async Task<ActionResult> UpsertTemplate([FromBody] UpsertStockPrintTemplateRequest req)
    {
        var branchId = GetBranchId();
        await _service.UpsertTemplateAsync(branchId, req);
        return Ok(new { message = "Template saved" });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteTemplate(Guid id)
    {
        await _service.DeleteTemplateAsync(id);
        return Ok(new { message = "Template deleted" });
    }

    [HttpPost("preview")]
    public async Task<ActionResult> Preview([FromQuery] Guid stockTicketId)
    {
        var branchId = GetBranchId();
        var ticket = await _context.StockTickets
            .Include(t => t.Supplier)
            .FirstOrDefaultAsync(t => t.StockTicketId == stockTicketId && t.BranchId == branchId);
        if (ticket == null) return NotFound(new { message = "Stock ticket not found" });

        var branch = await _context.Branches.FindAsync(branchId);
        var ticketType = ticket.TicketType ?? "STOCK_CHECK";
        var template = await _service.ResolveTemplateAsync(branchId, ticketType);

        if (template == null)
        {
            return Content($"<html><body><p>No template configured for ticket type: {ticketType}</p></body></html>", "text/html");
        }

        var html = await _service.BuildTicketHtmlAsync(ticket, branch!, template);
        return Content(html, "text/html");
    }
}
