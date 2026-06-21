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
            {
                return BadRequest(new { message = "Mã OTP không chính xác hoặc đã hết hạn." });
            }

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

            var newBranch = new Branch
            {
                BranchId = Guid.NewGuid(),
                BranchName = dto.BranchName,
                Address = "Chưa cập nhật"
            };
            _context.Branches.Add(newBranch);

            var newUser = new User
            {
                UserId = Guid.NewGuid(),
                Username = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                IsActive = 1,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };

            newUser.Roles.Add(ownerRole);
            _context.Users.Add(newUser);

            var staffAssignment = new StaffAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = newUser.UserId,
                BranchId = newBranch.BranchId,
                AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };
            _context.StaffAssignments.Add(staffAssignment);

            otp.IsUsed = 1;
            await _context.SaveChangesAsync();

            // 🎯 ĐỒNG BỘ: Sinh Token có kèm BranchId cho Owner mới đăng ký
            var rolesList = new List<string> { ownerRole.RoleName };
            var jwtToken = _jwtHelper.GenerateToken(newUser.UserId, newUser.Username, newUser.FullName, rolesList, newBranch.BranchId.ToString());

            var responseDto = _mapper.Map<UserResponseDto>(newUser);
            responseDto.Token = jwtToken;
            responseDto.Roles = rolesList;

            return Ok(new
            {
                message = "Đăng ký doanh nghiệp và tài khoản quản trị tối cao thành công!",
                branchId = newBranch.BranchId,
                roles = rolesList, // Trả thêm mảng roles ra ngoài body để Front-end dễ check
                data = responseDto
            });
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

            // 🎯 LẤY DANH SÁCH ROLE THỰC TẾ (Sẽ ra "Owner" hoặc "Staff" tùy tài khoản)
            var userRoles = await _context.Roles
                .Where(r => r.Users.Any(u => u.UserId == user.UserId))
                .Select(r => r.RoleName)
                .ToListAsync();

            // Lấy chi nhánh làm việc
            var assignment = await _context.StaffAssignments
                .FirstOrDefaultAsync(sa => sa.UserId == user.UserId);

            if (assignment == null)
            {
                return BadRequest(new { message = "Tài khoản này chưa được phân công vào chi nhánh nào." });
            }

            // 🎯 ĐÚC TOKEN: Nhét cả danh sách Role thực tế + BranchId vào trong ruột Token
            var jwtToken = _jwtHelper.GenerateToken(user.UserId, user.Username, user.FullName, userRoles, assignment.BranchId.ToString());

            var responseDto = _mapper.Map<UserResponseDto>(user);
            responseDto.Token = jwtToken;
            responseDto.Roles = userRoles;

            return Ok(new
            {
                message = "Đăng nhập thành công!",
                branchId = assignment.BranchId,
                roles = userRoles, // Trả hẳn mảng danh sách vai trò về cho Front-end đọc trực tiếp không cần giải mã token
                data = responseDto
            });
        }

        // ==========================================
        // 4. CREATE STAFF: OWNER TẠO NHÂN VIÊN TRỰC TIẾP
        // ==========================================
        [Authorize(Roles = "Owner")]
        [HttpPost("create-staff")]
        public async Task<IActionResult> CreateStaffByOwner([FromBody] CreateStaffDto dto)
        {
            // 1. Đọc mã chi nhánh của chính Owner đang ra lệnh tạo staff này
            var branchIdClaim = User.FindFirst("branchId")?.Value;
            if (string.IsNullOrEmpty(branchIdClaim) || !Guid.TryParse(branchIdClaim, out Guid ownerBranchId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin chi nhánh của Owner." });
            }

            var isEmailExist = await _context.Users.AnyAsync(u => u.Email == dto.Email.Trim().ToLower());
            if (isEmailExist) return BadRequest(new { Message = "Email nhân viên này đã được sử dụng." });

            var staffRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Staff");
            if (staffRole == null)
            {
                staffRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Staff", Description = "Nhên viên cửa hàng" };
                _context.Roles.Add(staffRole);
            }

            var newStaff = new User
            {
                UserId = Guid.NewGuid(),
                Username = dto.Email.Trim().ToLower(),
                Email = dto.Email.Trim().ToLower(),
                FullName = dto.FullName.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                IsActive = 1,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };
            newStaff.Roles.Add(staffRole);
            _context.Users.Add(newStaff);

            var staffAssignment = new StaffAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = newStaff.UserId,
                BranchId = ownerBranchId, // Nhân viên thừa hưởng luôn chi nhánh của Owner quản lý
                AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };
            _context.StaffAssignments.Add(staffAssignment);

            await _context.SaveChangesAsync();
            return Ok(new { Message = $"Đã tạo thành công nhân viên {newStaff.FullName} thuộc chi nhánh của bạn!" });
        }
    }
}