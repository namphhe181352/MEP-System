using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MEP_Management_System.Services
{
    public class JwtHelper
    {
        private readonly IConfiguration _config;

        public JwtHelper(IConfiguration config)
        {
            _config = config;
        }

        // =========================================================================
        // HÀM SINH TOKEN: Đã thêm tham số permissions để hỗ trợ cơ chế RBAC phân quyền
        // =========================================================================
        public string GenerateToken(Guid userId, string username, string fullName, List<string> roles, List<string> permissions, string? branchId = null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.GivenName, fullName)
            };

            // 🎯 ĐẬP THÊM CLAIM BRANCH_ID VÀO ĐÂY NẾU CÓ (Giữ nguyên logic cũ của bạn)
            if (!string.IsNullOrEmpty(branchId))
            {
                // Dùng chuỗi thường "branchId" để đồng bộ tuyệt đối với hàm User.FindFirst("branchId") bên Controller
                claims.Add(new Claim("branchId", branchId));
            }

            // 1. Duyệt nạp toàn bộ danh sách Roles (Giữ nguyên logic cũ của bạn)
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // 2. 🌟 THÊM MỚI: Duyệt nạp toàn bộ danh sách mã quyền (PermissionCode) vào JWT dưới claim "permission"
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Giữ nguyên thiết lập cấu hình Token chạy thời hạn 1 năm (365 ngày) của bạn
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(365), // 🔥 Giữ nguyên 1 năm không lo hết hạn của bạn
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Hàm đọc và giải mã Token (Giữ nguyên hoàn toàn vì TokenValidationParameters tự động dịch ngược toàn bộ claims mới)
        public ClaimsPrincipal? ValidateAndReadToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]!);

            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _config["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _config["Jwt:Audience"],
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}