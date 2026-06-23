using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MEP_Management_System.Controllers.AdminController;

[ApiController]
[Route("api/admin/store-approvals")]
[Authorize(Roles = "Admin")]
[Tags("Admin (Admin)")]
public class StoreApprovalsController : ControllerBase
{
    private readonly MEPDatabaseContext _context;

    public StoreApprovalsController(MEPDatabaseContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string status = "PENDING",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _context.StoreApprovals
            .Include(s => s.User)
            .Include(s => s.ReviewedByUser)
            .Where(s => s.Status == status.ToUpper());

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(s => new StoreApprovalDto
        {
            ApprovalId = s.ApprovalId,
            UserId = s.UserId,
            StoreName = s.StoreName,
            BusinessLicense = s.BusinessLicense,
            TaxCode = s.TaxCode,
            StoreAddress = s.StoreAddress,
            DocumentUrls = s.DocumentUrls,
            Status = s.Status,
            RejectReason = s.RejectReason,
            ReviewedBy = s.ReviewedBy,
            ReviewedAt = s.ReviewedAt,
            CreatedAt = s.CreatedAt,
            OwnerEmail = s.User.Email,
            OwnerName = s.User.FullName,
            ReviewerName = s.ReviewedByUser?.FullName
        }).ToList();

        return Ok(new PageResultDto<StoreApprovalDto>
        {
            Items = dtos,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var s = await _context.StoreApprovals
            .Include(x => x.User)
            .Include(x => x.ReviewedByUser)
            .FirstOrDefaultAsync(x => x.ApprovalId == id);

        if (s == null) return NotFound(new { error = new { code = "MSG-404", message = "Not found." } });

        return Ok(new StoreApprovalDto
        {
            ApprovalId = s.ApprovalId,
            UserId = s.UserId,
            StoreName = s.StoreName,
            BusinessLicense = s.BusinessLicense,
            TaxCode = s.TaxCode,
            StoreAddress = s.StoreAddress,
            DocumentUrls = s.DocumentUrls,
            Status = s.Status,
            RejectReason = s.RejectReason,
            ReviewedBy = s.ReviewedBy,
            ReviewedAt = s.ReviewedAt,
            CreatedAt = s.CreatedAt,
            OwnerEmail = s.User.Email,
            OwnerName = s.User.FullName,
            ReviewerName = s.ReviewedByUser?.FullName
        });
    }

    [HttpPut("{id}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveStoreRequest req)
    {
        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var approval = await _context.StoreApprovals.Include(a => a.User).FirstOrDefaultAsync(a => a.ApprovalId == id);
        if (approval == null) return NotFound(new { error = new { code = "MSG-404", message = "Not found." } });
        if (approval.Status != "PENDING") return BadRequest(new { error = new { code = "MSG-400", message = "Chỉ duyệt đơn PENDING." } });

        approval.Status = "APPROVED";
        approval.ReviewedBy = adminId;
        approval.ReviewedAt = now;

        approval.User.IsVerified = true;

        var newBranch = new Branch
        {
            BranchId = Guid.NewGuid(),
            BranchName = approval.StoreName,
            Address = approval.StoreAddress ?? "Chưa cập nhật"
        };
        _context.Branches.Add(newBranch);

        var staffAssignment = new StaffAssignment
        {
            AssignmentId = Guid.NewGuid(),
            UserId = approval.UserId,
            BranchId = newBranch.BranchId,
            AssignedAt = now
        };
        _context.StaffAssignments.Add(staffAssignment);

        _context.SystemLogs.Add(new SystemLog
        {
            LogId = Guid.NewGuid(),
            UserId = adminId,
            Action = "APPROVE_STORE",
            TableName = "StoreApprovals",
            Description = $"Duyệt cửa hàng {approval.StoreName}. Notes: {req.Notes}",
            Level = "INFO",
            Source = "StoreApprovalsController",
            Timestamp = now
        });

        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã duyệt cửa hàng." });
    }

    [HttpPut("{id}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectStoreRequest req)
    {
        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var approval = await _context.StoreApprovals.FirstOrDefaultAsync(a => a.ApprovalId == id);
        if (approval == null) return NotFound(new { error = new { code = "MSG-404", message = "Not found." } });
        if (approval.Status != "PENDING") return BadRequest(new { error = new { code = "MSG-400", message = "Chỉ duyệt đơn PENDING." } });

        approval.Status = "REJECTED";
        approval.RejectReason = req.Reason;
        approval.ReviewedBy = adminId;
        approval.ReviewedAt = now;

        _context.SystemLogs.Add(new SystemLog
        {
            LogId = Guid.NewGuid(),
            UserId = adminId,
            Action = "REJECT_STORE",
            TableName = "StoreApprovals",
            Description = $"Từ chối cửa hàng {approval.StoreName}. Lý do: {req.Reason}",
            Level = "INFO",
            Source = "StoreApprovalsController",
            Timestamp = now
        });

        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã từ chối cửa hàng." });
    }
}
