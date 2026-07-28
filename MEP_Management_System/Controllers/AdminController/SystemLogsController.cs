using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using ClosedXML.Excel;

namespace MEP_Management_System.Controllers.AdminController;

[ApiController]
[Route("api/admin/system-logs")]
[Authorize(Roles = "Admin")]
[Tags("Admin (Admin)")]
public class SystemLogsController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly IMapper _mapper;

    public SystemLogsController(MEPDatabaseContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? action,
        [FromQuery] Guid? userId,
        [FromQuery] string? level,
        [FromQuery] string? source,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _context.SystemLogs
            .Include(l => l.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(action))
            query = query.Where(l => l.Action.Contains(action));

        if (userId.HasValue)
            query = query.Where(l => l.UserId == userId.Value);

        if (!string.IsNullOrEmpty(level))
            query = query.Where(l => l.Level == level.ToUpper());

        if (!string.IsNullOrEmpty(source))
            query = query.Where(l => l.Source != null && l.Source.Contains(source));

        if (fromDate.HasValue)
        {
            var from = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Unspecified);
            query = query.Where(l => l.Timestamp >= from);
        }

        if (toDate.HasValue)
        {
            var to = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Unspecified);
            query = query.Where(l => l.Timestamp <= to);
        }

        var totalCount = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PageResultDto<SystemLogDto>
        {
            Items = _mapper.Map<List<SystemLogDto>>(logs),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportLogs([FromQuery] string format = "txt")
    {
        var logs = await _context.SystemLogs
            .Include(l => l.User)
            .OrderByDescending(l => l.Timestamp)
            .Take(5000)
            .ToListAsync();

        if (format.ToLower() == "csv")
        {
            var csv = new StringBuilder();
            csv.AppendLine("LogId,Timestamp,Level,Source,Action,Description,UserEmail,IpAddress");
            foreach (var l in logs)
            {
                csv.AppendLine($"\"{l.LogId}\",\"{l.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{l.Level}\",\"{l.Source}\",\"{l.Action}\",\"{l.Description?.Replace("\"", "\"\"")}\",\"{l.User?.Email}\",\"{l.IpAddress}\"");
            }
            
            var contentBytes = Encoding.UTF8.GetBytes(csv.ToString());
            var bom = Encoding.UTF8.GetPreamble();
            var result = bom.Concat(contentBytes).ToArray();
            
            return File(result, "text/csv", $"system-logs-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        else if (format.ToLower() == "excel")
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("System Logs");
            
            // Add Header
            var headers = new string[] { "LogId", "Timestamp", "Level", "Source", "Action", "Description", "UserEmail", "IpAddress" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
            }
            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

            // Add Data
            for (int i = 0; i < logs.Count; i++)
            {
                var l = logs[i];
                int row = i + 2;
                worksheet.Cell(row, 1).Value = l.LogId.ToString();
                worksheet.Cell(row, 2).Value = $"{l.Timestamp:yyyy-MM-dd HH:mm:ss}";
                worksheet.Cell(row, 3).Value = l.Level;
                worksheet.Cell(row, 4).Value = l.Source;
                worksheet.Cell(row, 5).Value = l.Action;
                worksheet.Cell(row, 6).Value = l.Description;
                worksheet.Cell(row, 7).Value = l.User?.Email;
                worksheet.Cell(row, 8).Value = l.IpAddress;
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"system-logs-{DateTime.UtcNow:yyyyMMdd}.xlsx");
        }
        else
        {
            var lines = logs.Select(l =>
                $"{l.Timestamp:yyyy-MM-dd HH:mm:ss}|{l.Level ?? "INFO"}|{l.Source ?? "System"}|{l.Action}|{l.Description}");
            return File(Encoding.UTF8.GetBytes(string.Join("\n", lines)), "text/plain", $"system-logs-{DateTime.UtcNow:yyyyMMdd}.txt");
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLog(Guid id)
    {
        var log = await _context.SystemLogs
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.LogId == id);

        if (log == null) return NotFound(new { error = new { code = "MSG-404", message = "Không tìm thấy." } });

        return Ok(_mapper.Map<SystemLogDto>(log));
    }
}
