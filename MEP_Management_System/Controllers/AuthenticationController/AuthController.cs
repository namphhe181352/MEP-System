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
using Microsoft.AspNetCore.RateLimiting; // Rate Limit

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
        // 2. REGISTER VERIFY: XÁC THỰC OTP + TẠO CỬA HÀNG & OWNER
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
                IsVerified = true, // kích hoạt luôn
                CreatedAt = now
            };
            newUser.Roles.Add(ownerRole);
            _context.Users.Add(newUser);

            // Tạo Branch đầu tiên
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

            // 🎯 Gán DefaultBranchId cho user khi tạo
            newUser.DefaultBranchId = branch.BranchId;

            // Gán Owner vào Branch luôn
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
        // 3. LOGIN: ĐĂNG NHẬP (DÙNG CHUNG CHO CẢ OWNER & STAFF)
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

            string? branchIdForToken = null;
            object? branchesForResponse = null;

            if (isOwner)
            {
                // Owner: lấy tất cả branches
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

                // 🎯 Dùng DefaultBranchId từ DB
                if (user.DefaultBranchId != null)
                {
                    branchIdForToken = user.DefaultBranchId.ToString();
                }
            }
            else
            {
                // Staff: lấy 1 branch duy nhất, gán vào JWT
                var assignment = await _context.StaffAssignments
                    .FirstOrDefaultAsync(sa => sa.UserId == user.UserId);

                if (assignment == null)
                    return BadRequest(new { message = "Tài khoản chưa được phân công chi nhánh." });

                branchIdForToken = assignment.BranchId.ToString();
            }

            var jwtToken = _jwtHelper.GenerateToken(
                user.UserId,
                user.Username,
                user.FullName,
                userRoles,
                branchId: branchIdForToken  // null nếu Owner, có giá trị nếu Staff
            );

            var responseDto = _mapper.Map<UserResponseDto>(user);
            responseDto.Token = jwtToken;
            responseDto.Roles = userRoles;

            // Determine defaultBranchId
            Guid? defaultBranchId = user.DefaultBranchId;
            if (defaultBranchId == null && isOwner)
            {
                // If Owner has no default branch set, use first available branch
                if (branchesForResponse is List<dynamic> branchList && branchList.Count > 0)
                {
                    defaultBranchId = branchList[0].BranchId;
                }
            }

            return Ok(new
            {
                message = "Đăng nhập thành công!",
                roles = userRoles,
                branches = branchesForResponse, // Owner nhận list branches, Staff nhận null
                defaultBranchId = defaultBranchId, // Add this
                data = responseDto
            });
        }

       
    }
}