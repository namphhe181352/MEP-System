using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using MEP_Management_System.Models;
using MEP_Management_System.DTOs;
using MEP_Management_System.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace MEP_Management_System.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class authController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly IMapper _mapper;
        private readonly JwtHelper _jwtHelper;

        public authController(MEPDatabaseContext context, IMapper mapper, JwtHelper jwtHelper)
        {
            _context = context;
            _mapper = mapper;
            _jwtHelper = jwtHelper;
        }

        // ==========================================
        // 1. REGISTER START: KIỂM TRA EMAIL & SINH OTP
        // ==========================================
        [EnableRateLimiting("StrictLimit")]
        [HttpPost("register/start")]
        public async Task<IActionResult> RegisterStart([FromBody] RegisterStartDto dto)
        {
            var emailExists = await _context.Users.AnyAsync(x => x.Email == dto.Email);
            if (emailExists)
            {
                return BadRequest(new { message = "Email này đã được đăng ký tài khoản." });
            }

            var otpCode = new Random().Next(100000, 999999).ToString();

            var otpVerification = new OtpVerification
            {
                OtpId = Guid.NewGuid(),
                Email = dto.Email,
                OtpCode = otpCode,
                Purpose = "REGISTER",
                ExpiredAt = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(10), DateTimeKind.Unspecified),
                IsUsed = 0
            };

            _context.OtpVerifications.Add(otpVerification);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Mã xác thực OTP đã được tạo thành công. Vui lòng kiểm tra email của bạn.",
                otp = otpCode
            });
        }

        // ==========================================
        // 2. REGISTER VERIFY: XÁC THỰC OTP + TẠO CỬA HÀNG MẶC ĐỊNH & OWNER
        // ==========================================
        [HttpPost("register/verify")]
        public async Task<IActionResult> RegisterVerify([FromBody] RegisterVerifyDto dto)
        {
            var otp = await _context.OtpVerifications
                .FirstOrDefaultAsync(x => x.Email == dto.Email
                                       && x.OtpCode == dto.OtpCode
                                       && x.IsUsed == 0
                                       && x.ExpiredAt > DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified));

            if (otp == null)
                return BadRequest(new { message = "Mã OTP không chính xác hoặc đã hết hạn." });

            var ownerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Owner");
            if (ownerRole == null)
            {
                ownerRole = new Role
                {
                    RoleId = Guid.NewGuid(),
                    RoleName = "Owner",
                    Description = "Toàn quyền quản trị hệ thống chuỗi cửa hàng",
                    IsSystem = 1
                };
                _context.Roles.Add(ownerRole);
            }

            var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            // Tạo Owner
            var newUser = new User
            {
                UserId = Guid.NewGuid(),
                Username = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                IsActive = 1,
                IsVerified = true,
                CreatedAt = now
            };
            newUser.Roles.Add(ownerRole);
            _context.Users.Add(newUser);

            // Tạo đúng 1 Branch duy nhất (Hệ thống chạy Single-Branch ngầm)
            var branch = new Branch
            {
                BranchId = Guid.NewGuid(),
                BranchName = dto.BranchName,
                ManagerUserId = newUser.UserId,
                IsActive = 1,
                CreatedAt = now,
                UpdatedAt = now
            };
            _context.Branches.Add(branch);

            // Gán DefaultBranchId ngầm để phục vụ kiến trúc tương lai
            newUser.DefaultBranchId = branch.BranchId;

            // Vẫn lưu vào StaffAssignments để phục vụ phân quyền dữ liệu sau này
            _context.StaffAssignments.Add(new StaffAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = newUser.UserId,
                BranchId = branch.BranchId,
                AssignedAt = now
            });

            otp.IsUsed = 1;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đăng ký thành công! Bạn có thể đăng nhập ngay." });
        }

        // ==========================================
        // 3. LOGIN: ĐĂNG NHẬP ĐƠN GIẢN HÓA CÓ THU THẬP QUYỀN TRUY CẬP (RBAC FLOW)
        // ==========================================
        [EnableRateLimiting("StrictLimit")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == dto.Email);
            if (user == null) return BadRequest(new { message = "Email này chưa được đăng ký." });

            var isCorrectPassword = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!isCorrectPassword) return BadRequest(new { message = "Mật khẩu không chính xác." });

            if (user.IsActive != 1) return BadRequest(new { message = "Tài khoản của bạn hiện đang bị khóa." });
            if (!user.IsVerified) return BadRequest(new { message = "Tài khoản của bạn chưa được xác thực." });

            // 1. Lấy danh sách tên nhóm vai trò (Roles) gắn liền với User
            var userRoles = await _context.Roles
                .Where(r => r.Users.Any(u => u.UserId == user.UserId))
                .Select(r => r.RoleName)
                .ToListAsync();

            // 2. 🌟 THÊM MỚI: Truy vấn toàn bộ mã quyền (PermissionCode) thông qua các Roles của User
            var rolePermissions = await _context.Roles
                .Where(r => r.Users.Any(u => u.UserId == user.UserId))
                .SelectMany(r => r.Permissions)
                .Select(p => p.PermissionCode)
                .ToListAsync();

            // 3. 🌟 THÊM MỚI: Truy vấn mã quyền (PermissionCode) được chỉ định / gán trực tiếp cho User (bảng nối UserPermission)
            var userPermissions = await _context.UserPermissions
                .Where(up => up.UserId == user.UserId)
                .Select(up => up.Permission.PermissionCode)
                .ToListAsync();

            // 4. 🌟 THÊM MỚI: Sử dụng phương thức tập hợp Union để gộp 2 mảng quyền lại và triệt tiêu trùng lặp ngầm bằng Distinct
            var allPermissions = rolePermissions
                .Union(userPermissions)
                .Distinct()
                .ToList();

            // 5. 🌟 THAY ĐỔI: Tiến hành truyền thêm tham số danh sách mảng quyền `allPermissions` vào hàm sinh Token
            var jwtToken = _jwtHelper.GenerateToken(
                user.UserId,
                user.Username,
                user.FullName,
                userRoles,
                allPermissions,
                branchId: null
            );

            var responseDto = _mapper.Map<UserResponseDto>(user);
            responseDto.Token = jwtToken;
            responseDto.Roles = userRoles;

            return Ok(new
            {
                message = "Đăng nhập thành công!",
                roles = userRoles,
                data = responseDto
            });
        }
    }
}