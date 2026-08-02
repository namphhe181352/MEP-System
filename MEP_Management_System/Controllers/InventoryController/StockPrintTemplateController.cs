using System;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.InventoryController;

[ApiController]
[Route("api/inventory/print-templates/stock-ticket")]
public class StockPrintTemplateController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly StockPrintTemplateService _service;

    public StockPrintTemplateController(MEPDatabaseContext context, StockPrintTemplateService service)
    {
        _context = context;
        _service = service;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found"));

    // GET /api/inventory/print-templates/stock-ticket?ticketType=...
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

    // PUT /api/inventory/print-templates/stock-ticket
    [HttpPut]
    public async Task<ActionResult> UpsertTemplate([FromBody] UpsertStockPrintTemplateRequest req)
    {
        var branchId = GetBranchId();
        var existing = await _context.StockPrintTemplates
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.TicketType == req.TicketType);

        if (existing != null)
        {
            existing.HeaderText = req.HeaderText;
            existing.FooterText = req.FooterText;
            existing.ShowLogo = req.ShowLogo;
            existing.LogoUrl = req.LogoUrl;
            existing.BranchName = req.BranchName;
            existing.BranchAddress = req.BranchAddress;
            existing.Phone = req.Phone;
            existing.TaxCode = req.TaxCode;
            existing.PaperSize = req.PaperSize ?? "A5";
            existing.FontSize = req.FontSize > 0 ? req.FontSize : 12;
            existing.FontFamily = req.FontFamily ?? "monospace";
            existing.ShowSupplier = req.ShowSupplier;
            existing.ShowBranchInfo = req.ShowBranchInfo;
            existing.ShowSignature = req.ShowSignature;
            existing.CustomFieldsJson = req.CustomFieldsJson ?? "{}";
            existing.IsActive = req.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.StockPrintTemplates.Add(new StockPrintTemplate
            {
                TemplateId = Guid.NewGuid(),
                BranchId = branchId,
                TicketType = req.TicketType,
                HeaderText = req.HeaderText,
                FooterText = req.FooterText,
                ShowLogo = req.ShowLogo,
                LogoUrl = req.LogoUrl,
                BranchName = req.BranchName,
                BranchAddress = req.BranchAddress,
                Phone = req.Phone,
                TaxCode = req.TaxCode,
                PaperSize = req.PaperSize ?? "A5",
                FontSize = req.FontSize > 0 ? req.FontSize : 12,
                FontFamily = req.FontFamily ?? "monospace",
                ShowSupplier = req.ShowSupplier,
                ShowBranchInfo = req.ShowBranchInfo,
                ShowSignature = req.ShowSignature,
                CustomFieldsJson = req.CustomFieldsJson ?? "{}",
                IsActive = req.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Template saved" });
    }

    // DELETE /api/inventory/print-templates/stock-ticket/{id}
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteTemplate(Guid id)
    {
        var template = await _context.StockPrintTemplates.FindAsync(id);
        if (template == null) return NotFound(new { message = "Template not found" });
        _context.StockPrintTemplates.Remove(template);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Template deleted" });
    }

    // POST /api/inventory/print-templates/stock-ticket/preview?stockTicketId=...
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
            // Return default HTML if no template configured
            return Content($"<html><body><p>No template configured for ticket type: {ticketType}</p></body></html>", "text/html");
        }

        var html = await _service.BuildTicketHtmlAsync(ticket, branch!, template);
        return Content(html, "text/html");
    }
}

public class UpsertStockPrintTemplateRequest
{
    public string TicketType { get; set; } = "STOCK_CHECK";
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public bool ShowLogo { get; set; } = false;
    public string? LogoUrl { get; set; }
    public string? BranchName { get; set; }
    public string? BranchAddress { get; set; }
    public string? Phone { get; set; }
    public string? TaxCode { get; set; }
    public string? PaperSize { get; set; }
    public int FontSize { get; set; } = 12;
    public string? FontFamily { get; set; }
    public bool ShowSupplier { get; set; } = true;
    public bool ShowBranchInfo { get; set; } = true;
    public bool ShowSignature { get; set; } = true;
    public string? CustomFieldsJson { get; set; }
    public bool IsActive { get; set; } = true;
}
