using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.OwnerController;

[ApiController]
[Route("api/owner/staffs")]
[Authorize(Roles = "Owner")]
[Tags("Owner (Owner)")]
public class OwnerStaffController : ControllerBase
{
    private readonly MEPDatabaseContext _context;

    public OwnerStaffController(MEPDatabaseContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy User ID của Owner hiện tại từ token
    /// </summary>
    private Guid GetOwnerUserIdFromToken()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var userId))
            throw new UnauthorizedAccessException("User claim is missing or invalid.");
        return userId;
    }

    /// <summary>
    /// Lấy danh sách Branch ID mà Owner quản lý (thông qua StaffAssignments)
    /// </summary>
    private async Task<List<Guid>> GetOwnerBranchIds(Guid ownerUserId)
    {
        return await _context.StaffAssignments
            .Where(sa => sa.UserId == ownerUserId)
            .Select(sa => sa.BranchId)
            .Distinct()
            .ToListAsync();
    }


    // ================================================
    // GET /api/owner/staffs
    // Lấy danh sách nhân viên do Owner tạo
    // Filter theo CreatedBy == ownerUserId (không quan tâm StaffAssignments)
    // Hỗ trợ tìm kiếm, phân trang, lọc theo branchId (nếu có)
    // ================================================
    [HttpGet]
    public async Task<IActionResult> GetStaffs(
        [FromQuery] string? search,
        [FromQuery] Guid? branchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var ownerUserId = GetOwnerUserIdFromToken();

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            // LOGIC MỚI: Lấy tất cả User có CreatedBy == ownerUserId
            var query = _context.Users
                .Include(u => u.StaffAssignments).ThenInclude(sa => sa.Branch)
                .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
                .Where(u => u.CreatedBy == ownerUserId)
                .AsQueryable();

            // Nếu Front-End truyền bộ lọc Chi nhánh, chỉ lấy nhân viên được gán chi nhánh đó
            if (branchId.HasValue)
            {
                query = query.Where(u => u.StaffAssignments.Any(sa => sa.BranchId == branchId.Value));
            }

            // Tìm kiếm theo tên hoặc email
            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalized = search.Trim().ToLower();
                query = query.Where(u =>
                    u.FullName.ToLower().Contains(normalized) ||
                    u.Email.ToLower().Contains(normalized));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(u => u.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new OwnerStaffDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    IsActive = u.IsActive,
                    // Lấy bản ghi chi nhánh mới nhất (OrderByDescending AssignedAt)
                    BranchId = u.StaffAssignments
                        .OrderByDescending(sa => sa.AssignedAt)
                        .Select(sa => (Guid?)sa.BranchId)
                        .FirstOrDefault(),
                    BranchName = u.StaffAssignments
                        .OrderByDescending(sa => sa.AssignedAt)
                        .Select(sa => sa.Branch.BranchName)
                        .FirstOrDefault(),
                    // Danh sách các quyền của nhân viên
                    PasswordHash = u.PasswordHash,
                    PermissionCodes = u.UserPermissions
                        .Select(up => up.Permission.PermissionCode)
                        .ToList()
                })
                .ToListAsync();

            return Ok(new ApiResponse<PaginatedResponse<OwnerStaffDto>>
            {
                Success = true,
                Message = "Danh sách nhân viên được tải thành công.",
                Data = new PaginatedResponse<OwnerStaffDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    HasNextPage = page * pageSize < totalCount,
                    HasPreviousPage = page > 1
                }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không có quyền truy cập.",
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi hệ thống.",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    // ================================================
    // GET /api/owner/staffs/{id}
    // Xem chi tiết nhân viên (với kiểm tra bảo mật CreatedBy)
    // ================================================
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStaffById(Guid id)
    {
        try
        {
            var ownerUserId = GetOwnerUserIdFromToken();

            // Kiểm tra nhân viên có CreatedBy == ownerUserId hay không (bảo mật)
            var staff = await _context.Users
                .Include(u => u.StaffAssignments).ThenInclude(sa => sa.Branch)
                .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
                .FirstOrDefaultAsync(u =>
                    u.UserId == id &&
                    u.CreatedBy == ownerUserId);

            if (staff == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Nhân viên không tồn tại hoặc không thuộc Owner này."
                });

            // Lấy chi nhánh mới nhất từ StaffAssignments
            var branchAssignment = staff.StaffAssignments
                .OrderByDescending(sa => sa.AssignedAt)
                .FirstOrDefault();

            return Ok(new ApiResponse<OwnerStaffDto>
            {
                Success = true,
                Message = "Thông tin nhân viên đã được tải.",
                Data = new OwnerStaffDto
                {
                    UserId = staff.UserId,
                    FullName = staff.FullName,
                    Email = staff.Email,
                    PhoneNumber = staff.PhoneNumber,
                    IsActive = staff.IsActive,
                    BranchId = branchAssignment?.BranchId,
                    BranchName = branchAssignment?.Branch?.BranchName,
                    PasswordHash = staff.PasswordHash,
                    PermissionCodes = staff.UserPermissions
                        .Select(up => up.Permission.PermissionCode)
                        .ToList()
                }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không có quyền truy cập.",
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi hệ thống.",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    // ================================================
    // POST /api/owner/staffs
    // Tạo nhân viên mới — gán CreatedBy = ownerUserId
    // BranchId là tùy chọn. Nếu có, sẽ thêm vào StaffAssignments
    // ================================================
    [HttpPost]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffWithPermissionRequest dto)
    {
        try
        {
            var ownerUserId = GetOwnerUserIdFromToken();

            // Validate dữ liệu bắt buộc
            if (string.IsNullOrWhiteSpace(dto.Username) ||
                string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.FullName) ||
                string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Thiếu thông tin bắt buộc.",
                    Errors = new List<string> { "Username, Email, FullName và Password là bắt buộc." }
                });

            var normalizedEmail = dto.Email.Trim().ToLower();

            // Kiểm tra email đã tồn tại
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Email đã được sử dụng."
                });

            // Kiểm tra username đã tồn tại
            if (await _context.Users.AnyAsync(u => u.Username == dto.Username.Trim()))
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Username đã tồn tại."
                });
            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber) &&
                   await _context.Users.AnyAsync(u => u.PhoneNumber == dto.PhoneNumber.Trim()))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Số điện thoại này đã được sử dụng bởi một tài khoản khác."
                });
            }
            // Nếu có truyền branchId, kiểm tra chi nhánh có thuộc quyền quản lý của Owner không
            if (dto.BranchId.HasValue)
            {
                var ownerBranchIds = await GetOwnerBranchIds(ownerUserId);
                if (!ownerBranchIds.Contains(dto.BranchId.Value))
                    return Forbid();

                var branchExists = await _context.Branches
                    .AnyAsync(b => b.BranchId == dto.BranchId.Value && b.IsActive == 1);
                if (!branchExists)
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Chi nhánh không hợp lệ hoặc đã bị vô hiệu hóa."
                    });
            }

            var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            // Tạo User mới
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Username = dto.Username.Trim(),
                Email = normalizedEmail,
                FullName = dto.FullName.Trim(),
                PhoneNumber = dto.PhoneNumber?.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password.Trim()),
                IsActive = 1,
                IsVerified = true,
                Status = "ACTIVE",
                CreatedAt = now,
                CreatedBy = ownerUserId  // GÁN OWNER ID VÀO CREATEDBY
            };

            // Xử lý Role
            var staffRoleName = string.IsNullOrWhiteSpace(dto.DefaultRoleType)
                ? "Staff"
                : dto.DefaultRoleType.Trim();

            var staffRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == staffRoleName);
            if (staffRole == null)
            {
                staffRole = new Role
                {
                    RoleId = Guid.NewGuid(),
                    RoleName = staffRoleName,
                    Description = staffRoleName == "SalesStaff" ? "Nhân viên bán hàng" :
                                  staffRoleName == "InventoryStaff" ? "Nhân viên kho" : "Nhân viên cửa hàng",
                    IsSystem = 1
                };
                _context.Roles.Add(staffRole);
            }
            user.Roles.Add(staffRole);

            // Xử lý Permission
            var permissionCodes = new List<string>();
            if (!string.IsNullOrWhiteSpace(dto.DefaultRoleType))
            {
                if (dto.DefaultRoleType.Equals("SalesStaff", StringComparison.OrdinalIgnoreCase))
                {
                    // Cập nhật theo các mã quyền mới bạn vừa nạp vào DB
                    permissionCodes.AddRange(new[] { "POS_SALE", "RETURN_PROCESS", "CUSTOMER_MANAGE" });
                }
                else if (dto.DefaultRoleType.Equals("InventoryStaff", StringComparison.OrdinalIgnoreCase))
                {
                    // Cập nhật theo các mã quyền mới bạn vừa nạp vào DB
                    permissionCodes.AddRange(new[] { "STOCK_VIEW", "STOCK_MANAGE", "STOCK_TRANSFER" });
                }
                else
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "DefaultRoleType không hợp lệ.",
                        Errors = new List<string> { "Chỉ được phép là 'SalesStaff' hoặc 'InventoryStaff'." }
                    });
            }
            else
            {
                permissionCodes = dto.CustomPermissionCodes?
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();
            }

            // TRANSACTION: Đảm bảo toàn vẹn dữ liệu
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.Users.Add(user);

                // Gán chi nhánh nếu có truyền vào
                if (dto.BranchId.HasValue)
                {
                    _context.StaffAssignments.Add(new StaffAssignment
                    {
                        AssignmentId = Guid.NewGuid(),
                        UserId = user.UserId,
                        BranchId = dto.BranchId.Value,
                        AssignedAt = now
                    });
                }

                // Validate và gán Permission
                if (permissionCodes.Any())
                {
                    var permissions = await _context.Permissions
                        .Where(p => permissionCodes.Contains(p.PermissionCode))
                        .ToListAsync();

                    var missing = permissionCodes
                        .Except(permissions.Select(p => p.PermissionCode), StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (missing.Any())
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Một hoặc nhiều mã quyền không hợp lệ.",
                            Errors = missing.Select(c => $"PermissionCode không tồn tại: {c}").ToList()
                        });
                    }

                    foreach (var permission in permissions)
                    {
                        _context.UserPermissions.Add(new UserPermission
                        {
                            UserPermissionId = Guid.NewGuid(),
                            UserId = user.UserId,
                            PermissionId = permission.PermissionId
                        });
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Lấy tên chi nhánh nếu có
                string? branchName = dto.BranchId.HasValue
                    ? await _context.Branches
                        .Where(b => b.BranchId == dto.BranchId.Value)
                        .Select(b => b.BranchName)
                        .FirstOrDefaultAsync()
                    : null;

                return CreatedAtAction(nameof(GetStaffById), new { id = user.UserId },
                    new ApiResponse<OwnerStaffDto>
                    {
                        Success = true,
                        Message = "Nhân viên được tạo thành công.",
                        Data = new OwnerStaffDto
                        {
                            UserId = user.UserId,
                            FullName = user.FullName,
                            Email = user.Email,
                            PhoneNumber = user.PhoneNumber,
                            IsActive = user.IsActive,
                            BranchId = dto.BranchId,
                            BranchName = branchName,
                            PasswordHash = user.PasswordHash,
                            PermissionCodes = permissionCodes
                        }
                    });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không có quyền truy cập.",
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi hệ thống khi tạo nhân viên.",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    // ================================================
    // PUT /api/owner/staffs/{id}
    // Cập nhật thông tin nhân viên (check CreatedBy)
    // Hỗ trợ cập nhật: FullName, Email, PhoneNumber, BranchId, PermissionCodes, IsActive
    // ================================================
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateStaff(Guid id, [FromBody] UpdateStaffPermissionRequest dto)
    {
        try
        {
            var ownerUserId = GetOwnerUserIdFromToken();

            // Kiểm tra nhân viên có thuộc Owner này hay không
            var staff = await _context.Users
                .Include(u => u.StaffAssignments)
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u =>
                    u.UserId == id &&
                    u.CreatedBy == ownerUserId);

            if (staff == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Nhân viên không tồn tại hoặc không thuộc Owner này."
                });

            var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            var ownerBranchIds = await GetOwnerBranchIds(ownerUserId);

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Cập nhật thông tin cơ bản
                if (!string.IsNullOrWhiteSpace(dto.FullName))
                    staff.FullName = dto.FullName.Trim();

                if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
                {
                    var phoneNumber = dto.PhoneNumber.Trim();
                    if (await _context.Users.AnyAsync(u => u.PhoneNumber == phoneNumber && u.UserId != staff.UserId))
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Số điện thoại này đã được sử dụng bởi một tài khoản khác."
                        });
                    }

                    staff.PhoneNumber = phoneNumber;
                }

                if (!string.IsNullOrWhiteSpace(dto.Email))
                {
                    var normalizedEmail = dto.Email.Trim().ToLower();

                    // Kiểm tra email không trùng với user khác
                    if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail && u.UserId != id))
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Email này đã được sử dụng bởi user khác."
                        });
                    }

                    staff.Email = normalizedEmail;
                }

                if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
                    staff.PhoneNumber = dto.PhoneNumber.Trim();

                if (dto.IsActive.HasValue)
                    staff.IsActive = dto.IsActive.Value;

                if (!string.IsNullOrWhiteSpace(dto.Password))
                {
                    staff.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password.Trim());
                }

                // Cập nhật chi nhánh nếu có truyền
                if (dto.BranchId.HasValue)
                {
                    // Kiểm tra chi nhánh có thuộc Owner không
                    if (!ownerBranchIds.Contains(dto.BranchId.Value))
                    {
                        await transaction.RollbackAsync();
                        return Forbid();
                    }

                    // Kiểm tra chi nhánh tồn tại và đang hoạt động
                    var branchExists = await _context.Branches
                        .AnyAsync(b => b.BranchId == dto.BranchId.Value && b.IsActive == 1);
                    if (!branchExists)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Chi nhánh không hợp lệ hoặc đã bị vô hiệu hóa."
                        });
                    }

                    // Kiểm tra nhân viên đã được gán chi nhánh này chưa
                    var alreadyAssigned = staff.StaffAssignments
                        .Any(sa => sa.BranchId == dto.BranchId.Value);

                    // Nếu chưa gán, thêm bản ghi mới
                    if (!alreadyAssigned)
                    {
                        _context.StaffAssignments.Add(new StaffAssignment
                        {
                            AssignmentId = Guid.NewGuid(),
                            UserId = staff.UserId,
                            BranchId = dto.BranchId.Value,
                            AssignedAt = now
                        });
                    }
                }

                // Cập nhật danh sách quyền (xóa cũ, thêm mới)
                if (dto.PermissionCodes != null)
                {
                    var normalizedCodes = dto.PermissionCodes
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Select(c => c.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    // Validate mã quyền tồn tại
                    var permissions = await _context.Permissions
                        .Where(p => normalizedCodes.Contains(p.PermissionCode))
                        .ToListAsync();

                    var missing = normalizedCodes
                        .Except(permissions.Select(p => p.PermissionCode), StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (missing.Any())
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Một hoặc nhiều mã quyền không hợp lệ.",
                            Errors = missing.Select(c => $"PermissionCode không tồn tại: {c}").ToList()
                        });
                    }

                    // Xóa quyền cũ
                    _context.UserPermissions.RemoveRange(staff.UserPermissions);

                    // Thêm quyền mới
                    foreach (var permission in permissions)
                    {
                        _context.UserPermissions.Add(new UserPermission
                        {
                            UserPermissionId = Guid.NewGuid(),
                            UserId = staff.UserId,
                            PermissionId = permission.PermissionId
                        });
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Cập nhật nhân viên thành công.",
                    Data = null
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không có quyền truy cập.",
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi hệ thống.",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    // ================================================
    // POST /api/owner/staffs/{id}/assign-branch
    // Gán chi nhánh cho nhân viên do Owner tạo
    // ================================================
    [HttpPost("{id:guid}/assign-branch")]
    public async Task<IActionResult> AssignBranch(Guid id, [FromBody] AssignStaffBranchRequest dto)
    {
        try
        {
            var ownerUserId = GetOwnerUserIdFromToken();
            var ownerBranchIds = await GetOwnerBranchIds(ownerUserId);

            var staff = await _context.Users
                .Include(u => u.StaffAssignments)
                .FirstOrDefaultAsync(u => u.UserId == id && u.CreatedBy == ownerUserId);

            if (staff == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Nhân viên không tồn tại hoặc không thuộc Owner này."
                });

            if (!ownerBranchIds.Contains(dto.BranchId))
                return Forbid();

            var branchExists = await _context.Branches
                .AnyAsync(b => b.BranchId == dto.BranchId && b.IsActive == 1);
            if (!branchExists)
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Chi nhánh không hợp lệ hoặc đã bị vô hiệu hóa."
                });

            if (staff.StaffAssignments.Any(sa => sa.BranchId == dto.BranchId))
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Nhân viên đã được gán vào chi nhánh này." 
                });

            _context.StaffAssignments.Add(new StaffAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = staff.UserId,
                BranchId = dto.BranchId,
                AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            });

            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Gán chi nhánh cho nhân viên thành công.",
                Data = null
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không có quyền truy cập.",
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi hệ thống.",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    // ================================================
    // DELETE /api/owner/staffs/{id}/unassign-branch/{branchId}
    // Hủy gán chi nhánh cho nhân viên
    // ================================================
    [HttpDelete("{id:guid}/unassign-branch/{branchId:guid}")]
    public async Task<IActionResult> UnassignBranch(Guid id, Guid branchId)
    {
        try
        {
            var ownerUserId = GetOwnerUserIdFromToken();
            var ownerBranchIds = await GetOwnerBranchIds(ownerUserId);

            var staff = await _context.Users
                .Include(u => u.StaffAssignments)
                .FirstOrDefaultAsync(u => u.UserId == id && u.CreatedBy == ownerUserId);

            if (staff == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Nhân viên không tồn tại hoặc không thuộc Owner này."
                });

            if (!ownerBranchIds.Contains(branchId))
                return Forbid();

            var assignment = staff.StaffAssignments.FirstOrDefault(sa => sa.BranchId == branchId);
            if (assignment == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Nhân viên không được gán vào chi nhánh này."
                });

            _context.StaffAssignments.Remove(assignment);
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Hủy gán chi nhánh cho nhân viên thành công.",
                Data = null
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không có quyền truy cập.",
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi hệ thống.",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    // ================================================
    // PUT /api/owner/staffs/{id}/toggle-status
    // Bật/tắt trạng thái IsActive của nhân viên (0 hoặc 1)
    // ================================================
    [HttpPut("{id:guid}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        try
        {
            var ownerUserId = GetOwnerUserIdFromToken();

            // Kiểm tra nhân viên có thuộc Owner này hay không
            var staff = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == id &&
                    u.CreatedBy == ownerUserId);

            if (staff == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Nhân viên không tồn tại hoặc không thuộc Owner này."
                });

            // Toggle IsActive (1 <-> 0)
            staff.IsActive = staff.IsActive == 1 ? (short)0 : (short)1;
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = $"Nhân viên đã được {(staff.IsActive == 1 ? "kích hoạt" : "vô hiệu hóa")} thành công.",
                Data = null
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không có quyền truy cập.",
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi hệ thống.",
                Errors = new List<string> { ex.Message }
            });
        }
    }
}