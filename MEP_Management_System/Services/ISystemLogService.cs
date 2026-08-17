using Microsoft.EntityFrameworkCore;
using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;

namespace MEP_Management_System.Services;

public interface ISystemLogService
{
    Task<PageResultDto<SystemLogDto>> GetLogsAsync(
        string? action,
        Guid? userId,
        string? level,
        string? source,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize);

    Task<SystemLogDto?> GetLogByIdAsync(Guid id);

    Task<List<SystemLog>> GetLogsForExportAsync(int take = 5000);

    Task<SystemLog> LogAsync(
        Guid userId,
        string action,
        string description,
        string? tableName = null,
        string? entityType = null,
        string? level = "INFO",
        string? source = null,
        string? ipAddress = null,
        string? userAgent = null);
}

public class SystemLogService : ISystemLogService
{
    private readonly MEPDatabaseContext _context;
    private readonly IMapper _mapper;

    public SystemLogService(MEPDatabaseContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PageResultDto<SystemLogDto>> GetLogsAsync(
        string? action,
        Guid? userId,
        string? level,
        string? source,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize)
    {
        var query = _context.SystemLogs
            .Include(l => l.User)
                .ThenInclude(u => u.Roles)
            .Include(l => l.User)
                .ThenInclude(u => u.StaffAssignments)
                    .ThenInclude(sa => sa.Branch)
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
            .Select(l => new SystemLogDto
            {
                LogId = l.LogId,
                Action = l.Action,
                Description = l.Description,
                TableName = l.TableName,
                EntityType = l.EntityType,
                Level = l.Level,
                Source = l.Source,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                UserName = l.User != null ? l.User.FullName : null,
                BranchName = l.User != null && !l.User.Roles.Any(r => r.RoleName == "Admin")
                    ? l.User.StaffAssignments.Select(sa => sa.Branch.BranchName).FirstOrDefault()
                    : null,
                RoleName = l.User != null ? l.User.Roles.Select(r => r.RoleName).FirstOrDefault() : null,
                Timestamp = l.Timestamp
            })
            .ToListAsync();

        return new PageResultDto<SystemLogDto>
        {
            Items = logs,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<SystemLogDto?> GetLogByIdAsync(Guid id)
    {
        return await _context.SystemLogs
            .Include(l => l.User)
                .ThenInclude(u => u.Roles)
            .Include(l => l.User)
                .ThenInclude(u => u.StaffAssignments)
                    .ThenInclude(sa => sa.Branch)
            .Select(l => new SystemLogDto
            {
                LogId = l.LogId,
                Action = l.Action,
                Description = l.Description,
                TableName = l.TableName,
                EntityType = l.EntityType,
                Level = l.Level,
                Source = l.Source,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                UserName = l.User != null ? l.User.FullName : null,
                BranchName = l.User != null && !l.User.Roles.Any(r => r.RoleName == "Admin")
                    ? l.User.StaffAssignments.Select(sa => sa.Branch.BranchName).FirstOrDefault()
                    : null,
                RoleName = l.User != null ? l.User.Roles.Select(r => r.RoleName).FirstOrDefault() : null,
                Timestamp = l.Timestamp
            })
            .FirstOrDefaultAsync(l => l.LogId == id);
    }

    public async Task<List<SystemLog>> GetLogsForExportAsync(int take = 5000)
    {
        return await _context.SystemLogs
            .Include(l => l.User)
            .OrderByDescending(l => l.Timestamp)
            .Take(take)
            .ToListAsync();
    }

    public async Task<SystemLog> LogAsync(
        Guid userId,
        string action,
        string description,
        string? tableName = null,
        string? entityType = null,
        string? level = "INFO",
        string? source = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var log = new SystemLog
        {
            LogId = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            Description = description,
            TableName = tableName,
            EntityType = entityType,
            Level = level,
            Source = source,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow
        };

        _context.SystemLogs.Add(log);
        await _context.SaveChangesAsync();

        return log;
    }
}
