using System;
using System.Threading.Tasks;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

public class InvoicePrintTemplateService
{
    private readonly MEPDatabaseContext _context;

    public InvoicePrintTemplateService(MEPDatabaseContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Resolve template: branch row first, fallback to global.
    /// </summary>
    public async Task<InvoicePrintTemplate?> ResolveTemplateAsync(Guid branchId)
    {
        var template = await _context.InvoicePrintTemplates
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.IsActive);

        return template ?? await _context.InvoicePrintTemplates
            .FirstOrDefaultAsync(t => t.BranchId == null && t.IsActive);
    }
}
