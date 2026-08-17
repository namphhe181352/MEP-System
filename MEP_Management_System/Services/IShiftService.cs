using MEP_Management_System.Exceptions;
using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MEP_Management_System.Services;

public interface IShiftService
{
    Task<ShiftDto> StartShiftAsync(CreateShiftRequest request, Guid userId, Guid branchId, string? userName);
    Task<ShiftDto?> EndShiftAsync(Guid shiftId, EndShiftRequest request, Guid userId, Guid branchId, string? userName);
    Task<ShiftSummaryDto?> GetShiftSummaryAsync(Guid shiftId, Guid branchId);
    Task<DTOs.PageResultDto<ShiftDto>> GetShiftsAsync(DateTime? from, DateTime? to, string? status, Guid? filterUserId, Guid branchId, int page, int pageSize);
}

public class ShiftService : IShiftService
{
    private readonly MEPDatabaseContext _context;
    private readonly IMapper _mapper;
    private readonly MepNotificationService _notifications;
    private readonly ISystemLogService _logService;

    public ShiftService(MEPDatabaseContext context, IMapper mapper, MepNotificationService notifications, ISystemLogService logService)
    {
        _context = context;
        _mapper = mapper;
        _notifications = notifications;
        _logService = logService;
    }

    private void LogAction(Guid userId, string action, string tableName, string entityType, Guid? entityId, string description)
    {
        _context.SystemLogs.Add(new SystemLog
        {
            LogId = Guid.NewGuid(),
            UserId = userId,
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

    public async Task<ShiftDto> StartShiftAsync(CreateShiftRequest request, Guid userId, Guid branchId, string? userName)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var branchOpenShift = await _context.Shifts
                .FirstOrDefaultAsync(s => s.BranchId == branchId && s.Status == "OPEN");

            if (branchOpenShift != null)
            {
                if ((DateTime.UtcNow - branchOpenShift.StartedAt).TotalHours > 24)
                {
                    var completedInvoices = await _context.Invoices
                        .Where(i => i.ShiftId == branchOpenShift.ShiftId && i.Status == "Completed")
                        .Include(i => i.Payments)
                        .ToListAsync();

                    var shiftRefunds = await _context.RefundRecords
                        .Where(r => r.Status == "REFUNDED" && r.CreatedAt >= branchOpenShift.StartedAt && r.CreatedAt <= DateTime.UtcNow &&
                            (r.ShiftId == branchOpenShift.ShiftId || (r.ShiftId == null && r.UserId == branchOpenShift.UserId)))
                        .ToListAsync();
                    var totalRefundAmount = shiftRefunds.Sum(r => r.RefundAmount);
                    var cashRefundAmount = shiftRefunds.Where(r => r.RefundMethod == "CASH").Sum(r => r.RefundAmount);

                    branchOpenShift.TotalRevenue = completedInvoices.Sum(i => i.TotalAmount) - totalRefundAmount;
                    branchOpenShift.TotalOrders = completedInvoices.Count;
                    var allPayments = completedInvoices.SelectMany(i => i.Payments).Where(p => p.Method == "Cash" && p.Status == "Confirmed").ToList();
                    var staleExchangeCashPayments = await _context.Payments
                        .Where(p => p.ShiftId == branchOpenShift.ShiftId && p.Method == "Cash" && p.Status == "Confirmed")
                        .ToListAsync();
                    allPayments = allPayments.Concat(staleExchangeCashPayments).ToList();
                    branchOpenShift.ExpectedCash = branchOpenShift.OpeningBalance + allPayments.Sum(p => p.Amount) - cashRefundAmount;
                    branchOpenShift.ActualCash = branchOpenShift.ExpectedCash;
                    branchOpenShift.Variance = 0;
                    branchOpenShift.Note = "Hệ thống tự động chốt ca do đã mở quá 24h";
                    branchOpenShift.EndedAt = DateTime.UtcNow;
                    branchOpenShift.Status = "CLOSED";
                    branchOpenShift.ClosedByUserId = userId;

                    await _context.SaveChangesAsync();
                    LogAction(userId, "TU_DONG_CHOT_CA", "shifts", "Shift", branchOpenShift.ShiftId,
                        $"Tự động chốt ca {branchOpenShift.ShiftCode} (quá 24h) | Người mở: {branchOpenShift.User?.FullName ?? branchOpenShift.UserId.ToString()} | Người kích hoạt: {userName ?? userId.ToString()}");

                    _ = _notifications.NotifyBranchShiftClosed(branchId, branchOpenShift.ShiftId.ToString(), new
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
                    throw new BusinessException("Chi nhánh đang có ca bán hoạt động. Vui lòng chốt ca trước khi mở ca mới.");
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
                BranchId = branchId,
                Status = "OPEN",
                StartedAt = DateTime.UtcNow,
                OpeningBalance = request.OpeningBalance
            };

            _context.Shifts.Add(shift);
            LogAction(userId, "MO_CA", "shifts", "Shift", shift.ShiftId,
                $"Mở ca {shift.ShiftCode} | Số dư đầu: {shift.OpeningBalance:N0} VND | Người mở: {userName ?? "Unknown"}");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _ = _notifications.NotifyBranchShiftOpened(branchId, new
            {
                shift.ShiftId,
                shift.ShiftCode,
                shift.StartedAt,
                shift.OpeningBalance,
                OpenedByUserId = userId
            });

            return new ShiftDto
            {
                ShiftId = shift.ShiftId,
                ShiftCode = shift.ShiftCode,
                UserId = shift.UserId,
                BranchId = shift.BranchId,
                Status = shift.Status,
                StartedAt = shift.StartedAt,
                OpeningBalance = shift.OpeningBalance,
                UserName = userName
            };
        }
        catch (BusinessException) { throw; }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            throw new BusinessException("Chi nhánh đang có ca bán hoạt động. Vui lòng chốt ca trước khi mở ca mới.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ShiftDto?> EndShiftAsync(Guid shiftId, EndShiftRequest request, Guid userId, Guid branchId, string? userName)
    {
        var shift = await _context.Shifts
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.ShiftId == shiftId && s.BranchId == branchId);
        if (shift == null) return null;
        if (shift.Status == "CLOSED") throw new BusinessException("Shift already closed");

        shift.ClosedByUserId = userId;
        if (shift.UserId != userId)
        {
            if (string.IsNullOrWhiteSpace(request.ForceCloseReason))
                throw new BusinessException("Vui lòng nhập lý do chốt hộ.");
            shift.ForceCloseReason = request.ForceCloseReason;
        }

        var completedInvoices = await _context.Invoices
            .Where(i => i.ShiftId == shiftId && i.Status == "Completed" && i.CreatedAt >= shift.StartedAt && i.CreatedAt <= DateTime.UtcNow)
            .Include(i => i.Payments)
            .ToListAsync();

        var shiftRefunds = await _context.RefundRecords
            .Where(r => r.Status == "REFUNDED" && r.CreatedAt >= shift.StartedAt && r.CreatedAt <= DateTime.UtcNow &&
                (r.ShiftId == shiftId || (r.ShiftId == null && r.UserId == shift.UserId)))
            .ToListAsync();

        var totalRefundAmount = shiftRefunds.Sum(r => r.RefundAmount);
        var cashRefundAmount = shiftRefunds.Where(r => r.RefundMethod == "CASH").Sum(r => r.RefundAmount);

        shift.TotalRevenue = completedInvoices.Sum(i => i.TotalAmount) - totalRefundAmount;
        shift.TotalOrders = completedInvoices.Count;
        var allPayments = completedInvoices.SelectMany(i => i.Payments).Where(p => p.Method == "Cash" && p.Status == "Confirmed").ToList();
        var exchangeCashPayments = await _context.Payments
            .Where(p => p.ShiftId == shiftId && p.Method == "Cash" && p.Status == "Confirmed")
            .ToListAsync();
        allPayments = allPayments.Concat(exchangeCashPayments).ToList();
        shift.ExpectedCash = shift.OpeningBalance + allPayments.Sum(p => p.Amount) - cashRefundAmount;
        shift.ActualCash = request.ActualCash;
        shift.Variance = request.ActualCash - shift.ExpectedCash;
        shift.Note = shift.UserId != userId && !string.IsNullOrWhiteSpace(shift.ForceCloseReason)
            ? (request.Note ?? "") + " [Chốt hộ: " + shift.ForceCloseReason + "]"
            : request.Note;
        shift.EndedAt = DateTime.UtcNow;
        shift.Status = "CLOSED";

        var chotHo = shift.UserId != userId;
        var nguoiMo = shift.User?.FullName ?? shift.UserId.ToString();
        LogAction(userId, chotHo ? "CHOT_CA_HO" : "CHOT_CA", "shifts", "Shift", shift.ShiftId,
            $"Chốt ca {shift.ShiftCode} | Doanh thu: {shift.TotalRevenue:N0} VND | Chênh lệch: {shift.Variance:N0} VND | Người mở: {nguoiMo}" +
            (chotHo ? $" | Người chốt hộ: {userName ?? userId.ToString()}" : ""));

        var varianceValue = shift.Variance ?? 0m;
        if (varianceValue != 0m)
        {
            var sign = varianceValue > 0 ? "thừa" : "thiếu";
            _context.Notifications.Add(new Notification
            {
                NotificationId = Guid.NewGuid(),
                Type = NotificationType.Announcement,
                RecipientUserId = null,
                BranchId = branchId,
                ReferenceId = shift.ShiftId,
                ReferenceType = "Shift",
                TriggeredByUserId = userId,
                Message = $"Ca {shift.ShiftCode} chênh lệch tiền: {sign} {Math.Abs(varianceValue):N0}₫",
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await _context.SaveChangesAsync();

        var dto = _mapper.Map<ShiftDto>(shift);
        dto.ClosedByUserName = userName;

        _ = _notifications.NotifyBranchShiftClosed(branchId, shift.ShiftId.ToString(), new
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
            ClosedBy = userName
        });

        return dto;
    }

    public async Task<ShiftSummaryDto?> GetShiftSummaryAsync(Guid shiftId, Guid branchId)
    {
        var shift = await _context.Shifts
            .Include(s => s.User)
            .Include(s => s.ClosedBy)
            .FirstOrDefaultAsync(s => s.ShiftId == shiftId && s.BranchId == branchId);
        if (shift == null) return null;

        var isOpen = shift.Status == "OPEN";
        DateTime? filterEnd = isOpen ? DateTime.UtcNow : shift.EndedAt;

        var completedInvoices = await _context.Invoices
            .Where(i => i.ShiftId == shiftId && i.Status == "Completed" && i.CreatedAt >= shift.StartedAt &&
                (!filterEnd.HasValue || i.CreatedAt <= filterEnd.Value))
            .Include(i => i.Payments)
            .Include(i => i.User)
            .Include(i => i.Order).ThenInclude(o => o.Customer)
            .ToListAsync();

        var payments = completedInvoices.SelectMany(i => i.Payments).ToList();

        var exchangePayments = await _context.Payments
            .Where(p => p.ShiftId == shiftId && p.Status == "Confirmed")
            .ToListAsync();
        payments = payments.Concat(exchangePayments).ToList();

        var shiftRefunds = await _context.RefundRecords
            .Include(r => r.User)
            .Include(r => r.Customer)
            .Where(r => r.Status == "REFUNDED" &&
                (r.ShiftId == shiftId ||
                 (r.ShiftId == null && r.UserId == shift.UserId && r.CreatedAt >= shift.StartedAt && (shift.EndedAt == null || r.CreatedAt <= shift.EndedAt))))
            .ToListAsync();

        var exchangeReturns = await _context.ReturnOrders
            .Include(r => r.User)
            .Include(r => r.Order).ThenInclude(o => o.Customer)
            .Include(r => r.ReturnItems).ThenInclude(ri => ri.BranchProduct).ThenInclude(bp => bp.Product)
            .Include(r => r.ExchangeItems).ThenInclude(ei => ei.BranchProduct).ThenInclude(bp => bp.Product)
            .Where(r => r.ReturnType == "EXCHANGE" && r.Order != null && r.Order.Invoice != null && r.Order.Invoice.ShiftId == shiftId)
            .ToListAsync();

        var totalRefundAmount = shiftRefunds.Sum(r => r.RefundAmount);
        var cashRefundAmount = shiftRefunds.Where(r => r.RefundMethod == "CASH").Sum(r => r.RefundAmount);
        var transferRefundAmount = shiftRefunds.Where(r => r.RefundMethod == "TRANSFER").Sum(r => r.RefundAmount);
        var exchangeDeltaIn = exchangeReturns.Sum(r => r.DeltaAmount ?? 0m);

        var realTimeRevenue = completedInvoices.Sum(i => i.TotalAmount) - totalRefundAmount + exchangeDeltaIn;
        var realTimeOrders = completedInvoices.Count;
        var realTimeCash = payments.Where(p => p.Method == "Cash" && p.Status == "Confirmed").Sum(p => p.Amount) - cashRefundAmount;
        var realTimeTransfer = payments.Where(p => p.Method == "Transfer" && p.Status == "Confirmed").Sum(p => p.Amount) - transferRefundAmount;

        var allUserIds = completedInvoices.Select(i => i.UserId).Concat(shiftRefunds.Select(r => r.UserId)).Distinct().ToList();
        var salesByUser = allUserIds.Select(uid => new ShiftSummaryDto.UserSalesSummary
        {
            UserId = uid,
            UserName = completedInvoices.FirstOrDefault(i => i.UserId == uid)?.User?.FullName
                      ?? shiftRefunds.FirstOrDefault(r => r.UserId == uid)?.User?.FullName
                      ?? "Unknown",
            InvoiceCount = completedInvoices.Count(i => i.UserId == uid),
            TotalAmount = completedInvoices.Where(i => i.UserId == uid).Sum(i => i.TotalAmount) - shiftRefunds.Where(r => r.UserId == uid).Sum(r => r.RefundAmount)
        }).OrderByDescending(s => s.TotalAmount).ToList();

        var activities = new List<ShiftSummaryDto.ShiftActivityDto>();
        foreach (var inv in completedInvoices)
        {
            activities.Add(new ShiftSummaryDto.ShiftActivityDto
            {
                Type = "Sale",
                Code = inv.InvoiceCode,
                Amount = inv.TotalAmount,
                Method = inv.Payments?.FirstOrDefault()?.Method,
                CustomerName = inv.Order?.Customer?.CustomerName,
                UserName = inv.User?.FullName,
                CreatedAt = inv.CreatedAt ?? DateTime.UtcNow,
            });
        }
        foreach (var r in shiftRefunds)
        {
            activities.Add(new ShiftSummaryDto.ShiftActivityDto
            {
                Type = "Refund",
                Code = r.RefundCode,
                Amount = -r.RefundAmount,
                Method = r.RefundMethod,
                CustomerName = r.Customer?.CustomerName,
                UserName = r.User?.FullName,
                CreatedAt = r.CreatedAt,
            });
        }
        foreach (var ex in exchangeReturns)
        {
            var fromNames = ex.ReturnItems?.Where(ri => ri.BranchProduct?.Product != null).Select(ri => ri.BranchProduct.Product.ProductName).Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>();
            var toNames = ex.ExchangeItems?.Where(ei => ei.BranchProduct?.Product != null).Select(ei => ei.BranchProduct.Product.ProductName).Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>();
            activities.Add(new ShiftSummaryDto.ShiftActivityDto
            {
                Type = "Exchange",
                Code = ex.ReturnCode,
                Amount = ex.DeltaAmount ?? 0m,
                Method = ex.PaymentMethod,
                CustomerName = ex.Order?.Customer?.CustomerName,
                UserName = ex.User?.FullName,
                Description = $"{string.Join(", ", fromNames)} → {string.Join(", ", toNames)}",
                CreatedAt = ex.CreatedAt ?? DateTime.UtcNow,
            });
        }
        activities = activities.OrderByDescending(a => a.CreatedAt).ToList();

        return new ShiftSummaryDto
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
            TotalRefunds = shiftRefunds.Count,
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
            SalesByUser = salesByUser,
            Activities = activities
        };
    }

    public async Task<DTOs.PageResultDto<ShiftDto>> GetShiftsAsync(DateTime? from, DateTime? to, string? status, Guid? filterUserId, Guid branchId, int page, int pageSize)
    {
        var query = _context.Shifts
            .Include(s => s.User)
            .Include(s => s.ClosedBy)
            .Where(s => s.BranchId == branchId)
            .AsQueryable();

        if (from.HasValue) query = query.Where(s => s.StartedAt >= from.Value);
        if (to.HasValue) query = query.Where(s => s.StartedAt <= to.Value);
        if (!string.IsNullOrEmpty(status)) query = query.Where(s => s.Status == status);
        if (filterUserId.HasValue) query = query.Where(s => s.UserId == filterUserId.Value);

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

        return new DTOs.PageResultDto<ShiftDto>
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }
}
