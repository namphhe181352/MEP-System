using MEP_Management_System.Services;
using MEP_Management_System.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using ClosedXML.Excel;

namespace MEP_Management_System.Controllers.AdminController;

[ApiController]
[Route("api/admin/system-logs")]
[Authorize(Roles = "Admin")]
[Tags("Admin (Admin)")]
public class SystemLogsController : ControllerBase
{
    private readonly ISystemLogService _logService;

    public SystemLogsController(ISystemLogService logService)
    {
        _logService = logService;
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
        var result = await _logService.GetLogsAsync(action, userId, level, source, fromDate, toDate, page, pageSize);
        return Ok(result);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportLogs([FromQuery] string format = "txt")
    {
        var logs = await _logService.GetLogsForExportAsync(5000);

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

            var headers = new string[] { "LogId", "Timestamp", "Level", "Source", "Action", "Description", "UserEmail", "IpAddress" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
            }
            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

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
        var log = await _logService.GetLogByIdAsync(id);
        if (log == null)
            return NotFound(new { error = new { code = "MSG-404", message = "Không tìm thấy." } });

        return Ok(log);
    }
}
