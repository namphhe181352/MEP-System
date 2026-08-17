using MEP_Management_System.DTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.OwnerServices;

public class OwnerAccountService : IOwnerAccountService
{
    private readonly MEPDatabaseContext _context;

    public OwnerAccountService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<PageResultDto<StaffAccountDto>> GetOwnersAsync(string? search, string? status, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Users
            .Include(u => u.Roles)
            .Include(u => u.StaffAssignments)
                .ThenInclude(sa => sa.Branch)
            .Where(u => u.Roles.Any(r => r.RoleName == "Owner"))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => u.IsActive == 1);
            else
                query = query.Where(u => u.Roles.Any(r => r.RoleName == status));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.Email,
                u.FullName,
                u.PhoneNumber,
                u.IsActive,
                u.IsVerified,
                u.Status,
                u.SuspendedUntil,
                u.SubscriptionPlan,
                Roles = u.Roles.Select(r => r.RoleName).ToList(),
                Assignment = u.StaffAssignments.FirstOrDefault(),
                u.CreatedAt
            })
            .ToListAsync();

        var result = items.Select(u => new StaffAccountDto
        {
            UserId = u.UserId,
            Username = u.Username,
            Email = u.Email,
            FullName = u.FullName,
            PhoneNumber = u.PhoneNumber,
            IsActive = u.IsActive ?? 0,
            IsVerified = u.IsVerified,
            Status = u.Status,
            SuspendedUntil = u.SuspendedUntil,
            SubscriptionPlan = u.SubscriptionPlan,
            Roles = u.Roles,
            CreatedAt = u.CreatedAt
        }).ToList();

        return new PageResultDto<StaffAccountDto>
        {
            Items = result,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<StaffAccountDto?> GetStaffByIdAsync(Guid id)
    {
        var staff = await _context.Users
            .Include(u => u.Roles)
            .Include(u => u.StaffAssignments)
                .ThenInclude(sa => sa.Branch)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (staff == null)
            throw new KeyNotFoundException("Tài khoản không tồn tại.");

        return new StaffAccountDto
        {
            UserId = staff.UserId,
            Username = staff.Username,
            Email = staff.Email,
            FullName = staff.FullName,
            PhoneNumber = staff.PhoneNumber,
            IsActive = staff.IsActive ?? 0,
            IsVerified = staff.IsVerified,
            Status = staff.Status,
            SuspendedUntil = staff.SuspendedUntil,
            SubscriptionPlan = staff.SubscriptionPlan,
            Roles = staff.Roles.Select(r => r.RoleName).ToList(),
            CreatedAt = staff.CreatedAt
        };
    }

    public async Task<Guid> CreateOwnerAsync(CreateOwnerRequest request)
    {
        var validRoles = new[] { "Owner" };
        if (!validRoles.Contains(request.Role))
            throw new BusinessException("MSG-400", $"Vai trò phải là: Owner.");

        if (await _context.Users.AnyAsync(u => u.Username == request.Username.Trim()))
            throw new BusinessException("MSG-409", "Username đã tồn tại.");

        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == request.Email.Trim().ToLower()))
            throw new BusinessException("MSG-409", "Email đã được sử dụng.");

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == request.Role);
        if (role == null)
            throw new KeyNotFoundException($"Vai trò '{request.Role}' không tồn tại.");

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = 1,
            IsVerified = request.Role == "Owner",
            Status = "ACTIVE",
            SubscriptionPlan = request.SubscriptionPlan,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        user.Roles.Add(role);

        if (request.Role == "Owner" && !request.BranchId.HasValue)
        {
            if (string.IsNullOrWhiteSpace(request.BranchName))
                throw new BusinessException("MSG-400", "Tên cửa hàng là bắt buộc khi tạo Owner.");

            var branch = new Branch
            {
                BranchId = Guid.NewGuid(),
                BranchName = request.BranchName.Trim()
            };
            _context.Branches.Add(branch);
            await _context.SaveChangesAsync();

            _context.StaffAssignments.Add(new StaffAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = user.UserId,
                BranchId = branch.BranchId,
                AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            });
        }
        else if (request.BranchId.HasValue)
        {
            _context.StaffAssignments.Add(new StaffAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = user.UserId,
                BranchId = request.BranchId.Value,
                AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            });
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user.UserId;
    }

    public async Task UpdateStaffAsync(Guid id, UpdateOwnerRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            throw new KeyNotFoundException("Tài khoản không tồn tại.");

        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == request.Email.Trim().ToLower() && u.UserId != id))
                throw new BusinessException("MSG-409", "Email đã được sử dụng.");
            user.Email = request.Email.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            user.PhoneNumber = request.PhoneNumber.Trim();

        if (!string.IsNullOrWhiteSpace(request.SubscriptionPlan))
            user.SubscriptionPlan = request.SubscriptionPlan;

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var newRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == request.Role);
            if (newRole == null)
                throw new KeyNotFoundException($"Vai trò '{request.Role}' không tồn tại.");
            user.Roles.Clear();
            user.Roles.Add(newRole);
        }

        await _context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(Guid id, string status)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            throw new KeyNotFoundException("Tài khoản không tồn tại.");

        if (status.ToLower() == "active")
        {
            user.IsActive = 1;
            user.Status = "ACTIVE";
        }
        else if (status.ToLower() == "suspended")
        {
            user.IsActive = 0;
            user.Status = "SUSPENDED";
        }
        else
        {
            throw new BusinessException("MSG-400", "Status phải là 'active' hoặc 'suspended'.");
        }

        await _context.SaveChangesAsync();
    }

    public async Task BanStaffAsync(Guid id, string reason, Guid? adminId)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            throw new KeyNotFoundException("Tài khoản không tồn tại.");

        user.IsActive = 0;
        user.Status = "BANNED";

        _context.SystemLogs.Add(new SystemLog
        {
            LogId = Guid.NewGuid(),
            UserId = adminId ?? Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Action = "BAN_STAFF",
            TableName = "users",
            EntityType = "User",
            Description = $"Ban tài khoản: {user.FullName} ({user.Email}). Lý do: {reason}",
            Level = "WARN",
            Source = "AdminService",
            Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });

        await _context.SaveChangesAsync();
    }
}
