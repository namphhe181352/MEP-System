using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/shifts")]
[Authorize]
public class ShiftsController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;
    private readonly IMapper _mapper;
    private readonly MepNotificationService _notifications;

    public ShiftsController(Models.MEPDatabaseContext context, IMapper mapper, MepNotificationService notifications)
    {
        _context = context;
        _mapper = mapper;
        _notifications = notifications;
    }

    private Guid? GetBranchId()
    {
        var val = User.FindFirst("branchId")?.Value;
        return string.IsNullOrEmpty(val) ? null : Guid.Parse(val);
    }
    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? throw new Exception("userId"));

    private void LogAction(string action, string tableName, string entityType, Guid? entityId, string description)
    {
        _context.SystemLogs.Add(new SystemLog
        {
            LogId = Guid.NewGuid(),
            UserId = GetUserId(),
            Action = action,
            TableName = tableName,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            Level = "INFO",
            Source = "ShiftManagement",
            Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
    }

    // POST /api/pos/shifts/start
    [HttpPost("start")]
    [HasPermission(PermissionConstants.SHIFT_CREATE)]
    public async Task<ActionResult<ShiftDto>> StartShift([FromBody] CreateShiftRequest request)
    {
        var userId = GetUserId();
        var branchId = GetBranchId();
        if (branchId == null) return BadRequest(ApiErrorResponse.Create("MSG-76", "Tài khoản chưa được phân công chi nhánh. Vui lòng liên hệ chủ cửa hàng."));

        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
            return BadRequest(ApiErrorResponse.Create("MSG-100", "Tài khoản không tồn tại."));
        if (user.IsActive == 0 || (user.Status != null && user.Status.ToUpper() != "ACTIVE"))
            return BadRequest(ApiErrorResponse.Create("MSG-101", "Tài khoản đang bị khoá/ẩn, không thể mở ca."));

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Chỉ kiểm tra Branch - Cash Session model: 1 Branch = 1 OPEN Shift
            var branchOpenShift = await _context.Shifts
                .FirstOrDefaultAsync(s => s.BranchId == branchId.Value && s.Status == "OPEN");

            if (branchOpenShift != null)
            {
                if ((DateTime.UtcNow - branchOpenShift.StartedAt).TotalHours > 24)
                {
                    // Auto-close stale shift regardless of who opened it
                    var completedInvoices = await _context.Invoices
                        .Where(i => i.ShiftId == branchOpenShift.ShiftId && i.Status == "Completed")
                        .Include(i => i.Payments)
                        .ToListAsync();

                    var shiftRefunds = await _context.RefundRecords
                        .Where(r => r.UserId == branchOpenShift.UserId && r.CreatedAt >= branchOpenShift.StartedAt && r.CreatedAt <= DateTime.UtcNow && r.Status == "REFUNDED")
                        .ToListAsync();
                    var totalRefundAmount = shiftRefunds.Sum(r => r.RefundAmount);
                    var cashRefundAmount = shiftRefunds.Where(r => r.RefundMethod == "CASH").Sum(r => r.RefundAmount);

                    branchOpenShift.TotalRevenue = completedInvoices.Sum(i => i.TotalAmount) - totalRefundAmount;
                    branchOpenShift.TotalOrders = completedInvoices.Count;
                    var allPayments = completedInvoices.SelectMany(i => i.Payments).Where(p => p.Method == "Cash" && p.Status == "Confirmed").ToList();
                    branchOpenShift.ExpectedCash = branchOpenShift.OpeningBalance + allPayments.Sum(p => p.Amount) - cashRefundAmount;
                    branchOpenShift.ActualCash = branchOpenShift.ExpectedCash;
                    branchOpenShift.Variance = 0;
                    branchOpenShift.Note = "Hệ thống tự động chốt ca do đã mở quá 24h";
                    branchOpenShift.EndedAt = DateTime.UtcNow;
                    branchOpenShift.Status = "CLOSED";
                    branchOpenShift.ClosedByUserId = userId;

                    await _context.SaveChangesAsync();
                    var nguoiAutoChot = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value ?? userId.ToString();
                    LogAction("TU_DONG_CHOT_CA", "shifts", "Shift", branchOpenShift.ShiftId,
                        $"Tự động chốt ca {branchOpenShift.ShiftCode} (quá 24h) | Người mở: {branchOpenShift.User?.FullName ?? branchOpenShift.UserId.ToString()} | Người kích hoạt: {nguoiAutoChot}");

                    _ = _notifications.NotifyBranchShiftClosed(branchId.Value, branchOpenShift.ShiftId.ToString(), new
                    {
                        branchOpenShift.ShiftId,
                        branchOpenShift.ShiftCode,
                        branchOpenShift.TotalRevenue,
                        branchOpenShift.TotalOrders,
                        branchOpenShift.Variance,
                        branchOpenShift.ExpectedCash,
                        branchOpenShift.ActualCash,
                        branchOpenShift.EndedAt
                    });
                }
                else
                {
                    await transaction.RollbackAsync();
                    return Conflict(ApiErrorResponse.Create("MSG-76", "Chi nhánh đang có ca bán hoạt động. Vui lòng chốt ca trước khi mở ca mới."));
                }
            }

            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            var todayCount = await _context.Shifts
                .Where(s => s.StartedAt >= today && s.StartedAt < tomorrow)
                .CountAsync();

            var shift = new Shift
            {
                ShiftId = Guid.NewGuid(),
                ShiftCode = CodeGenerator.GenerateShiftCode(todayCount + 1),
                UserId = userId,
                BranchId = branchId.Value,
                Status = "OPEN",
                StartedAt = DateTime.UtcNow,
                OpeningBalance = request.OpeningBalance
            };

            _context.Shifts.Add(shift);
            var nguoiMo = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value ?? "Unknown";
            LogAction("MO_CA", "shifts", "Shift", shift.ShiftId,
                $"Mở ca {shift.ShiftCode} | Số dư đầu: {shift.OpeningBalance:N0} VND | Người mở: {nguoiMo}");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var dto = new ShiftDto
            {
                ShiftId = shift.ShiftId,
                ShiftCode = shift.ShiftCode,
                UserId = shift.UserId,
                BranchId = shift.BranchId,
                Status = shift.Status,
                StartedAt = shift.StartedAt,
                OpeningBalance = shift.OpeningBalance,
                UserName = User.FindFirst(ClaimTypes.GivenName)?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value
            };

            _ = _notifications.NotifyBranchShiftOpened(branchId.Value, new
            {
                shift.ShiftId,
                shift.ShiftCode,
                shift.StartedAt,
                shift.OpeningBalance,
                OpenedByUserId = userId
            });

            return Ok(dto);
        }
catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            return Conflict(ApiErrorResponse.Create("MSG-76", "Chi nhánh đang có ca bán hoạt động. Vui lòng chốt ca trước khi mở ca mới."));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // POST /api/pos/shifts/{shiftId}/end
    [HttpPost("{shiftId:guid}/end")]
    [HasPermission(PermissionConstants.SHIFT_UPDATE)]
    public async Task<ActionResult<ShiftDto>> EndShift(Guid shiftId, [FromBody] EndShiftRequest request)
    {
        var userId = GetUserId();
        var branchId = GetBranchId();
        if (branchId == null) return BadRequest(ApiErrorResponse.Create("MSG-76", "Tài khoản chưa được phân công chi nhánh."));

        var shift = await _context.Shifts
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.ShiftId == shiftId && s.BranchId == branchId.Value);
        if (shift == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Shift not found"));
        if (shift.Status == "CLOSED") return BadRequest(ApiErrorResponse.Create("MSG-76", "Shift already closed"));

        // Luôn ghi nhận người chốt ca
        shift.ClosedByUserId = userId;

        // Nếu người chốt != người mở → chốt hộ, bắt buộc có lý do
        if (shift.UserId != userId)
        {
            if (string.IsNullOrWhiteSpace(request.ForceCloseReason))
                return BadRequest(ApiErrorResponse.Create("MSG-76", "Vui lòng nhập lý do chốt hộ."));

            shift.ForceCloseReason = request.ForceCloseReason;
        }

        // Calculate revenue from ALL invoices under this shift (by ShiftId)
        var completedInvoices = await _context.Invoices
            .Where(i => i.ShiftId == shiftId && i.Status == "Completed" && i.CreatedAt >= shift.StartedAt && i.CreatedAt <= DateTime.UtcNow)
            .Include(i => i.Payments)
            .ToListAsync();

        var shiftRefunds = await _context.RefundRecords
            .Where(r => r.UserId == shift.UserId && r.CreatedAt >= shift.StartedAt && r.CreatedAt <= DateTime.UtcNow && r.Status == "REFUNDED")
            .ToListAsync();
            
        var totalRefundAmount = shiftRefunds.Sum(r => r.RefundAmount);
        var cashRefundAmount = shiftRefunds.Where(r => r.RefundMethod == "CASH").Sum(r => r.RefundAmount);

        shift.TotalRevenue = completedInvoices.Sum(i => i.TotalAmount) - totalRefundAmount;
        shift.TotalOrders = completedInvoices.Count;
        var allPayments = completedInvoices.SelectMany(i => i.Payments).Where(p => p.Method == "Cash" && p.Status == "Confirmed").ToList();
        shift.ExpectedCash = shift.OpeningBalance + allPayments.Sum(p => p.Amount) - cashRefundAmount;
        shift.ActualCash = request.ActualCash;
        shift.Variance = request.ActualCash - shift.ExpectedCash;
        shift.Note = shift.UserId != userId && !string.IsNullOrWhiteSpace(shift.ForceCloseReason)
            ? (request.Note ?? "") + " [Chốt hộ: " + shift.ForceCloseReason + "]"
            : request.Note;
        shift.EndedAt = DateTime.UtcNow;
        shift.Status = "CLOSED";

        var chotHo = shift.UserId != userId;
        var nguoiChot = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value ?? userId.ToString();
        var nguoiMo = shift.User?.FullName ?? shift.UserId.ToString();
        LogAction(chotHo ? "CHOT_CA_HO" : "CHOT_CA", "shifts", "Shift", shift.ShiftId,
            $"Chốt ca {shift.ShiftCode} | Doanh thu: {shift.TotalRevenue:N0} VND | Chênh lệch: {shift.Variance:N0} VND | Người mở: {nguoiMo}" +
            (chotHo ? $" | Người chốt hộ: {nguoiChot}" : ""));

        await _context.SaveChangesAsync();

        var dto = _mapper.Map<ShiftDto>(shift);
        dto.ClosedByUserName = User.FindFirst(ClaimTypes.GivenName)?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value;

        _ = _notifications.NotifyBranchShiftClosed(branchId.Value, shift.ShiftId.ToString(), new
        {
            shift.ShiftId,
            shift.ShiftCode,
            shift.TotalRevenue,
            shift.TotalOrders,
            shift.Variance,
            shift.ExpectedCash,
            shift.ActualCash,
            shift.EndedAt,
            ForceClosed = shift.UserId != userId,
            ClosedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value
        });

        return Ok(dto);
    }

    // GET /api/pos/shifts/{shiftId}/summary
    [HttpGet("{shiftId:guid}/summary")]
    public async Task<ActionResult<ShiftSummaryDto>> GetShiftSummary(Guid shiftId)
    {
        var branchId = GetBranchId();
        if (branchId == null) return BadRequest(ApiErrorResponse.Create("MSG-76", "Tài khoản chưa được phân công chi nhánh."));

        var shift = await _context.Shifts
            .Include(s => s.User)
            .Include(s => s.ClosedBy)
            .FirstOrDefaultAsync(s => s.ShiftId == shiftId && s.BranchId == branchId.Value);
        if (shift == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Shift not found"));

        var isOpen = shift.Status == "OPEN";
        DateTime? filterEnd = isOpen ? DateTime.UtcNow : shift.EndedAt;

        // Query ALL invoices under this shift (by ShiftId)
        var completedInvoices = await _context.Invoices
            .Where(i => i.ShiftId == shiftId && i.Status == "Completed" && i.CreatedAt >= shift.StartedAt &&
                (!filterEnd.HasValue || i.CreatedAt <= filterEnd.Value))
            .Include(i => i.Payments)
            .Include(i => i.User)
            .ToListAsync();

        var payments = completedInvoices.SelectMany(i => i.Payments).ToList();

        var shiftRefunds = await _context.RefundRecords
            .Include(r => r.User)
            .Where(r => r.UserId == shift.UserId && r.CreatedAt >= shift.StartedAt && (shift.EndedAt == null || r.CreatedAt <= shift.EndedAt) && r.Status == "REFUNDED")
            .ToListAsync();

        var totalRefundAmount = shiftRefunds.Sum(r => r.RefundAmount);
        var cashRefundAmount = shiftRefunds.Where(r => r.RefundMethod == "CASH").Sum(r => r.RefundAmount);
        var transferRefundAmount = shiftRefunds.Where(r => r.RefundMethod == "TRANSFER").Sum(r => r.RefundAmount);

        var realTimeRevenue = completedInvoices.Sum(i => i.TotalAmount) - totalRefundAmount;
        var realTimeOrders = completedInvoices.Count;
        var realTimeCash = payments.Where(p => p.Method == "Cash" && p.Status == "Confirmed").Sum(p => p.Amount) - cashRefundAmount;
        var realTimeTransfer = payments.Where(p => p.Method == "Transfer" && p.Status == "Confirmed").Sum(p => p.Amount) - transferRefundAmount;

        // Sales by User breakdown (including refunds)
        var allUserIds = completedInvoices.Select(i => i.UserId).Concat(shiftRefunds.Select(r => r.UserId)).Distinct().ToList();
        
        var salesByUser = allUserIds.Select(uid => {
            var userInvoices = completedInvoices.Where(i => i.UserId == uid).ToList();
            var userRefunds = shiftRefunds.Where(r => r.UserId == uid).ToList();
            
            string userName = userInvoices.FirstOrDefault()?.User?.FullName 
                              ?? userRefunds.FirstOrDefault()?.User?.FullName 
                              ?? "Unknown";
            
            return new ShiftSummaryDto.UserSalesSummary
            {
                UserId = uid,
                UserName = userName,
                InvoiceCount = userInvoices.Count, // Only count sales orders as invoice count
                TotalAmount = userInvoices.Sum(i => i.TotalAmount) - userRefunds.Sum(r => r.RefundAmount)
            };
        })
        .OrderByDescending(s => s.TotalAmount)
        .ToList();

        var summary = new ShiftSummaryDto
        {
            ShiftId = shift.ShiftId,
            ShiftCode = shift.ShiftCode,
            StartedAt = shift.StartedAt,
            EndedAt = shift.EndedAt,
            OpeningBalance = shift.OpeningBalance,
            ClosingBalance = isOpen ? (shift.OpeningBalance + realTimeCash) : ((shift.OpeningBalance + (shift.TotalRevenue)) - (shift.Variance ?? 0)),
            TotalRevenue = isOpen ? realTimeRevenue : shift.TotalRevenue,
            TotalCash = isOpen ? realTimeCash : payments.Where(p => p.Method == "Cash").Sum(p => p.Amount),
            TotalTransfer = isOpen ? realTimeTransfer : payments.Where(p => p.Method == "Transfer").Sum(p => p.Amount),
            TotalDebt = 0,
            ExpectedCash = isOpen ? (shift.OpeningBalance + realTimeCash) : (shift.ExpectedCash ?? 0),
            ActualCash = shift.ActualCash,
            Variance = shift.Variance,
            TotalOrders = isOpen ? realTimeOrders : shift.TotalOrders,
            ReturnCount = shiftRefunds.Count,
            OpenedByUserId = shift.UserId,
            OpenedByUserName = shift.User?.FullName,
            ClosedByUserId = shift.ClosedByUserId,
            ClosedByUserName = shift.ClosedBy?.FullName,
            ForceCloseReason = shift.ForceCloseReason,
            Note = shift.Note,
            PaymentBreakdown = payments.GroupBy(p => p.Method).Select(g => new ShiftSummaryDto.PaymentMethodSummary
            {
                Method = g.Key,
                Total = g.Sum(p => p.Amount),
                Count = g.Count()
            }).ToList(),
            SalesByUser = salesByUser
        };

        return Ok(summary);
    }

    // GET /api/pos/shifts
    [HttpGet]
    public async Task<ActionResult<PageResultDto<ShiftDto>>> GetShifts(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? status, [FromQuery] Guid? filterUserId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var branchId = GetBranchId();
        if (branchId == null) return Ok(new PageResultDto<ShiftDto> { Items = new(), TotalCount = 0 });

        var query = _context.Shifts
            .Include(s => s.User)
            .Include(s => s.ClosedBy)
            .Where(s => s.BranchId == branchId.Value)
            .AsQueryable();

        if (from.HasValue) query = query.Where(s => s.StartedAt >= from.Value);
        if (to.HasValue) query = query.Where(s => s.StartedAt <= to.Value);
        
        if (!string.IsNullOrEmpty(status))
            query = query.Where(s => s.Status == status);
            
        // Nếu truyền filterUserId thì lọc theo user đó
        if (filterUserId.HasValue)
            query = query.Where(s => s.UserId == filterUserId.Value);

        var total = await query.CountAsync();
        var shifts = await query
            .OrderByDescending(s => s.StartedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        var items = shifts.Select(s =>
        {
            var dto = _mapper.Map<ShiftDto>(s);
            dto.ClosedByUserName = s.ClosedBy?.FullName;
            return dto;
        }).ToList();

        return Ok(new PageResultDto<ShiftDto>
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }
}