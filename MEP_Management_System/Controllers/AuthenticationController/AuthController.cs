using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MEP_Management_System.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class authController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly IMapper _mapper;
        private readonly JwtHelper _jwtHelper;
        private readonly IEmailService _emailService; // 🌟 Injection Email Service

        public authController(
            MEPDatabaseContext context,
            IMapper mapper,
            JwtHelper jwtHelper,
            IEmailService emailService) // 🌟 Khai báo thêm Email Service ở Constructor
        {
            _context = context;
            _mapper = mapper;
            _jwtHelper = jwtHelper;
            _emailService = emailService;
        }

        // ==========================================
        // 1. REGISTER START: KIỂM TRA EMAIL, SINH OTP & GỬI EMAIL THỰC TẾ
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

            // Sinh mã OTP gồm 6 chữ số ngẫu nhiên
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

            try
            {
                // 🌟 GỌI SERVICE GỬI MAIL THỰC TẾ Ở ĐÂY
                await _emailService.SendOtpEmailAsync(dto.Email, otpCode);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }

            return Ok(new
            {
                message = "Mã xác thực OTP đã được gửi đến email của bạn thành công. Vui lòng kiểm tra hộp thư."
                // 🔒 Lưu ý: Để bảo mật, trong môi trường Production thực tế, ta KHÔNG trả kèm thuộc tính `otp: otpCode` về Client nữa!
            });
        }

        // ==========================================
        // 2. REGISTER VERIFY: VÔ HIỆU HÓA TẠO OWNER TỰ ĐĂNG KÝ
        // ==========================================
        // Từ 2026-07-27: Tài khoản Owner CHỈ được tạo bởi Admin.
        // Endpoint này giữ để tương thích ngược nhưng không tạo Owner/Branch nữa.
        // Owner cần liên hệ Admin qua api /api/admin/accounts/owners.
        [HttpPost("register/verify")]
        public async Task<IActionResult> RegisterVerify([FromBody] RegisterVerifyDto dto)
        {
            // Đánh dấu OTP đã dùng (nếu có) để tránh reuse
            var otp = await _context.OtpVerifications
                .FirstOrDefaultAsync(x => x.Email == dto.Email
                                       && x.OtpCode == dto.OtpCode
                                       && x.IsUsed == 0
                                       && x.ExpiredAt > DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified));

            if (otp != null)
            {
                otp.IsUsed = 1;
                await _context.SaveChangesAsync();
            }

            return StatusCode(StatusCodes.Status410Gone, new
            {
                message = "Tính năng tự đăng ký tài khoản Owner đã ngừng hoạt động. Vui lòng liên hệ Admin để được tạo tài khoản."
            });
        }

        // ==========================================
        // 3. FORGOT PASSWORD START: GỬI OTP RESET PASSWORD
        // ==========================================
        [EnableRateLimiting("StrictLimit")]
        [HttpPost("forgot-password/start")]
        public async Task<IActionResult> ForgotPasswordStart([FromBody] ForgotPasswordStartDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == dto.Email);
            if (user == null)
                return BadRequest(new { message = "Email chưa được đăng ký." });

            if (user.IsActive != 1 || !user.IsVerified)
                return BadRequest(new { message = "Tài khoản đang bị khóa hoặc chưa xác minh." });

            // Vô hiệu hóa các OTP cũ của email này trước khi tạo mới
            var oldOtps = await _context.OtpVerifications
                .Where(x => x.Email == dto.Email && x.Purpose == "RESET_PASSWORD" && x.IsUsed == 0)
                .ToListAsync();
            foreach (var old in oldOtps) old.IsUsed = 1;

            var otpCode = new Random().Next(100000, 999999).ToString();
            var otpVerification = new OtpVerification
            {
                OtpId = Guid.NewGuid(),
                Email = dto.Email,
                OtpCode = otpCode,
                Purpose = "RESET_PASSWORD",
                ExpiredAt = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(10), DateTimeKind.Unspecified),
                IsUsed = 0
            };
            _context.OtpVerifications.Add(otpVerification);

            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendOtpEmailAsync(dto.Email, otpCode);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }

            return Ok(new { message = "Mã xác nhận đã được gửi đến email của bạn." });
        }

        // ==========================================
        // 4. FORGOT PASSWORD VERIFY: XÁC NHẬN OTP + ĐẶT LẠI PASSWORD
        // ==========================================
        [EnableRateLimiting("StrictLimit")]
        [HttpPost("forgot-password/verify")]
        public async Task<IActionResult> ForgotPasswordVerify([FromBody] ForgotPasswordVerifyDto dto)
        {
            var otp = await _context.OtpVerifications
                .FirstOrDefaultAsync(x => x.Email == dto.Email
                                       && x.OtpCode == dto.OtpCode
                                       && x.Purpose == "RESET_PASSWORD"
                                       && x.IsUsed == 0
                                       && x.ExpiredAt > DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified));

            if (otp == null)
                return BadRequest(new { message = "Mã xác nhận không hợp lệ hoặc đã hết hạn." });

            var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == dto.Email);
            if (user == null)
                return BadRequest(new { message = "Tài khoản không tồn tại." });

            otp.IsUsed = 1;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword.Trim());
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đặt lại mật khẩu thành công." });
        }

        // ==========================================
        // 5. LOGIN: ĐĂNG NHẬP ĐƠN GIẢN HÓA CÓ THU THẬP QUYỀN TRUY CẬP (RBAC FLOW)
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

            var userRoles = await _context.Roles
                .Where(r => r.Users.Any(u => u.UserId == user.UserId))
                .Select(r => r.RoleName)
                .ToListAsync();

            var isOwner = userRoles.Contains("Owner");
            var isAdmin = userRoles.Contains("Admin");

            string? branchIdForToken = null;
            object? branchesForResponse = null;

            // Admin: không gán branchId - toàn hệ thống
            if (isAdmin)
            {
                branchIdForToken = null;
                branchesForResponse = null;
            }
            else if (isOwner)
            {
                var branches = await _context.StaffAssignments
                    .Where(sa => sa.UserId == user.UserId)
                    .Include(sa => sa.Branch)
                    .Select(sa => new
                    {
                        sa.Branch.BranchId,
                        sa.Branch.BranchName,
                        sa.Branch.Address,
                        sa.Branch.City,
                        sa.Branch.Phone,
                        sa.Branch.IsActive
                    })
                    .ToListAsync();

                branchesForResponse = branches;

                if (user.DefaultBranchId != null)
                {
                    branchIdForToken = user.DefaultBranchId.ToString();
                }
                else if (branches.Any())
                {
                    branchIdForToken = branches.First().BranchId.ToString();
                }
                else
                {
                    var firstActiveBranch = await _context.Branches.FirstOrDefaultAsync(b => b.IsActive == 1);
                    if (firstActiveBranch != null)
                    {
                        branchIdForToken = firstActiveBranch.BranchId.ToString();
                    }
                }
            }
            else
            {
                // Sale, Warehouse, etc.: lấy branch từ StaffAssignments
                if (user.DefaultBranchId.HasValue)
                {
                    branchIdForToken = user.DefaultBranchId.Value.ToString();
                }
                else
                {
                    var assignment = await _context.StaffAssignments
                        .Where(sa => sa.UserId == user.UserId)
                        .OrderBy(sa => sa.AssignedAt)
                        .FirstOrDefaultAsync();

                    if (assignment == null)
                    {
                        var firstBranch = await _context.Branches.FirstOrDefaultAsync(b => b.IsActive == 1);
                        if (firstBranch != null)
                        {
                            user.DefaultBranchId = firstBranch.BranchId;
                            _context.StaffAssignments.Add(new StaffAssignment
                            {
                                AssignmentId = Guid.NewGuid(),
                                UserId = user.UserId,
                                BranchId = firstBranch.BranchId,
                                AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
                            });
                            await _context.SaveChangesAsync();
                            branchIdForToken = firstBranch.BranchId.ToString();
                        }
                        else
                        {
                            return BadRequest(new { message = "Tài khoản chưa được phân công chi nhánh." });
                        }
                    }
                    else
                    {
                        user.DefaultBranchId = assignment.BranchId;
                        await _context.SaveChangesAsync();
                        branchIdForToken = assignment.BranchId.ToString();
                    }
                }
            }

            var rolePermissions = await _context.Roles
                .Where(r => r.Users.Any(u => u.UserId == user.UserId))
                .SelectMany(r => r.Permissions)
                .Select(p => p.PermissionCode)
                .ToListAsync();

            var userPermissions = await _context.UserPermissions
                .Where(up => up.UserId == user.UserId)
                .Select(up => new { up.Permission.PermissionCode, up.IsGranted })
                .ToListAsync();

            // Deny list: permissions explicitly denied (IsGranted = false)
            var deniedPermissions = userPermissions
                .Where(up => up.IsGranted == false)
                .Select(up => up.PermissionCode)
                .ToHashSet();

            // Allowed list: role permissions, minus denied, plus explicitly allowed user permissions
            var allowedUserPermissions = userPermissions
                .Where(up => up.IsGranted != false)
                .Select(up => up.PermissionCode);

            var allPermissions = rolePermissions
                .Union(allowedUserPermissions)
                .Except(deniedPermissions)
                .Distinct()
                .ToList();

            var jwtToken = _jwtHelper.GenerateToken(
                user.UserId,
                user.Username,
                user.FullName,
                userRoles,
                allPermissions,
                branchId: branchIdForToken
            );

            var responseDto = _mapper.Map<UserResponseDto>(user);
            responseDto.Token = jwtToken;
            responseDto.Roles = userRoles;
            responseDto.Permissions = allPermissions;

            return Ok(new
            {
                message = "Đăng nhập thành công!",
                roles = userRoles,
                data = responseDto
            });
        }
    }
}