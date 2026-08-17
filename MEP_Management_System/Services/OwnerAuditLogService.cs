using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

public interface IOwnerAuditLogService
{
    Task<PageResultDto<SystemLogDto>> GetLogsAsync(
        Guid branchId,
        string? action,
        Guid? userId,
        string? level,
        string? entityType,
        DateTime? fromDate,
        DateTime? toDate,
        string? userName,
        int page,
        int pageSize);

    Task<SystemLogDto?> GetLogByIdAsync(Guid id, Guid branchId);
    Task<List<object>> GetAuditLogUsersAsync(Guid branchId);
}

public class OwnerAuditLogService : IOwnerAuditLogService
{
    private readonly MEPDatabaseContext _context;
    private readonly IMapper _mapper;

    public OwnerAuditLogService(MEPDatabaseContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PageResultDto<SystemLogDto>> GetLogsAsync(
        Guid branchId,
        string? action,
        Guid? userId,
        string? level,
        string? entityType,
        DateTime? fromDate,
        DateTime? toDate,
        string? userName,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var userIdsInBranch = await _context.StaffAssignments
            .Where(sa => sa.BranchId == branchId)
            .Select(sa => sa.UserId)
            .ToListAsync();

        if (!userIdsInBranch.Any())
        {
            return new PageResultDto<SystemLogDto>
            {
                Items = new List<SystemLogDto>(),
                TotalCount = 0,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        var query = _context.SystemLogs
            .Include(l => l.User)
            .Where(l => userIdsInBranch.Contains(l.UserId))
            .AsQueryable();

        if (!string.IsNullOrEmpty(action))
            query = query.Where(l => l.Action.Contains(action));

        if (userId.HasValue)
            query = query.Where(l => l.UserId == userId.Value);

        if (!string.IsNullOrEmpty(level))
            query = query.Where(l => l.Level == level.ToUpper());

        if (!string.IsNullOrEmpty(userName))
            query = query.Where(l => l.User.FullName.Contains(userName));

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(l => l.EntityType == entityType);

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

        return new PageResultDto<SystemLogDto>
        {
            Items = _mapper.Map<List<SystemLogDto>>(logs),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<SystemLogDto?> GetLogByIdAsync(Guid id, Guid branchId)
    {
        var userIdsInBranch = await _context.StaffAssignments
            .Where(sa => sa.BranchId == branchId)
            .Select(sa => sa.UserId)
            .ToListAsync();

        var log = await _context.SystemLogs
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.LogId == id && userIdsInBranch.Contains(l.UserId));

        return log == null ? null : _mapper.Map<SystemLogDto>(log);
    }

    public async Task<List<object>> GetAuditLogUsersAsync(Guid branchId)
    {
        var users = await _context.StaffAssignments
            .Where(sa => sa.BranchId == branchId)
            .Select(sa => sa.User)
            .Where(u => u.Status != "DELETED")
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Status,
                u.IsActive
            })
            .ToListAsync();

        return users.Cast<object>().ToList();
    }
}
