using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/shifts")]
[HasPermission(PermissionConstants.SHIFT_VIEW)]
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

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found"));
    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? throw new Exception("userId"));

    // POST /api/pos/shifts/start
    [HttpPost("start")]
    public async Task<ActionResult<ShiftDto>> StartShift([FromBody] CreateShiftRequest request)
    {
        var userId = GetUserId();
        var branchId = GetBranchId();

        var openShift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.UserId == userId && s.BranchId == branchId && s.Status == "OPEN");
        if (openShift != null) return Conflict(ApiErrorResponse.Create("MSG-76", "You already have an open shift"));

        var todayCount = await _context.Shifts
            .Where(s => s.BranchId == branchId && s.StartedAt.Date == DateTime.UtcNow.Date)
            .CountAsync();

        var shift = new Shift
        {
            ShiftId = Guid.NewGuid(),
            ShiftCode = CodeGenerator.GenerateShiftCode(todayCount + 1),
            UserId = userId,
            BranchId = branchId,
            Status = "OPEN",
            StartedAt = DateTime.UtcNow,
            OpeningBalance = request.OpeningBalance
        };

        _context.Shifts.Add(shift);
        await _context.SaveChangesAsync();

        var dto = new ShiftDto
        {
            ShiftId = shift.ShiftId,
            ShiftCode = shift.ShiftCode,
            UserId = shift.UserId,
            BranchId = shift.BranchId,
            Status = shift.Status,
            StartedAt = shift.StartedAt,
            OpeningBalance = shift.OpeningBalance,
            UserName = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value
        };

        // Real-time: notify branch staff that a new shift was opened
        _ = _notifications.NotifyBranchShiftOpened(branchId, new
        {
            shift.ShiftId,
            shift.ShiftCode,
            shift.StartedAt,
            shift.OpeningBalance,
            UserId = userId
        });

        return Ok(dto);
    }

    // POST /api/pos/shifts/{shiftId}/end
    [HttpPost("{shiftId:guid}/end")]
    public async Task<ActionResult<ShiftDto>> EndShift(Guid shiftId, [FromBody] EndShiftRequest request)
    {
        var userId = GetUserId();
        var branchId = GetBranchId();

        var shift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.ShiftId == shiftId && s.UserId == userId && s.BranchId == branchId);
        if (shift == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Shift not found"));
        if (shift.Status == "CLOSED") return BadRequest(ApiErrorResponse.Create("MSG-76", "Shift already closed"));

        // Calculate revenue from this shift's completed invoices
        var completedInvoices = await _context.Invoices
            .Where(i => i.UserId == userId && i.Status == "Completed" && i.CreatedAt >= shift.StartedAt && i.CreatedAt <= DateTime.UtcNow)
            .Include(i => i.Payments)
            .ToListAsync();

        shift.TotalRevenue = completedInvoices.Sum(i => i.TotalAmount);
        shift.TotalOrders = completedInvoices.Count;
        var allPayments = completedInvoices.SelectMany(i => i.Payments).Where(p => p.Method == "Cash" && p.Status == "Confirmed").ToList();
        shift.ExpectedCash = shift.OpeningBalance + allPayments.Sum(p => p.Amount);
        shift.ActualCash = request.ActualCash;
        shift.Variance = request.ActualCash - shift.ExpectedCash;
        shift.Note = request.Note;
        shift.EndedAt = DateTime.UtcNow;
        shift.Status = "CLOSED";

        await _context.SaveChangesAsync();

        var dto = _mapper.Map<ShiftDto>(shift);

        // Real-time: notify branch staff that shift was closed with summary
        _ = _notifications.NotifyBranchShiftClosed(branchId, shift.ShiftId.ToString(), new
        {
            shift.ShiftId,
            shift.ShiftCode,
            shift.TotalRevenue,
            shift.TotalOrders,
            shift.Variance,
            shift.ExpectedCash,
            shift.ActualCash,
            shift.EndedAt
        });

        return Ok(dto);
    }

    // GET /api/pos/shifts/{shiftId}/summary
    [HttpGet("{shiftId:guid}/summary")]
    public async Task<ActionResult<ShiftSummaryDto>> GetShiftSummary(Guid shiftId)
    {
        var userId = GetUserId();
        var branchId = GetBranchId();

        var shift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.ShiftId == shiftId && s.UserId == userId && s.BranchId == branchId);
        if (shift == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Shift not found"));

        // Calculate real-time if shift is OPEN, otherwise use saved values
        var isOpen = shift.Status == "OPEN";
        DateTime? filterEnd = isOpen ? DateTime.UtcNow : shift.EndedAt;

        var completedInvoices = await _context.Invoices
            .Where(i => i.UserId == userId && i.Status == "Completed" && i.CreatedAt >= shift.StartedAt &&
                (!filterEnd.HasValue || i.CreatedAt <= filterEnd.Value))
            .Include(i => i.Payments)
            .ToListAsync();

        var payments = completedInvoices.SelectMany(i => i.Payments).ToList();

        // Calculate real-time values
        var realTimeRevenue = completedInvoices.Sum(i => i.TotalAmount);
        var realTimeOrders = completedInvoices.Count;
        var realTimeCash = payments.Where(p => p.Method == "Cash" && p.Status == "Confirmed").Sum(p => p.Amount);
        var realTimeTransfer = payments.Where(p => p.Method == "Transfer" && p.Status == "Confirmed").Sum(p => p.Amount);
        var realTimeDebt = payments.Where(p => p.Method == "Debt" && p.Status == "Confirmed").Sum(p => p.Amount);

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
            TotalDebt = isOpen ? realTimeDebt : payments.Where(p => p.Method == "Debt").Sum(p => p.Amount),
            ExpectedCash = isOpen ? (shift.OpeningBalance + realTimeCash) : (shift.ExpectedCash ?? 0),
            ActualCash = shift.ActualCash,
            Variance = shift.Variance,
            TotalOrders = isOpen ? realTimeOrders : shift.TotalOrders,
            PaymentBreakdown = payments.GroupBy(p => p.Method).Select(g => new ShiftSummaryDto.PaymentMethodSummary
            {
                Method = g.Key,
                Total = g.Sum(p => p.Amount),
                Count = g.Count()
            }).ToList()
        };

        return Ok(summary);
    }

    // GET /api/pos/shifts
    [HttpGet]
    public async Task<ActionResult<PageResultDto<ShiftDto>>> GetShifts(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        var branchId = GetBranchId();

        var query = _context.Shifts
            .Where(s => s.UserId == userId && s.BranchId == branchId)
            .AsQueryable();

        if (from.HasValue) query = query.Where(s => s.StartedAt >= from.Value);
        if (to.HasValue) query = query.Where(s => s.StartedAt <= to.Value);

        var total = await query.CountAsync();
        var shifts = await query
            .OrderByDescending(s => s.StartedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return Ok(new PageResultDto<ShiftDto>
        {
            Items = shifts.Select(_mapper.Map<ShiftDto>).ToList(),
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }
}
