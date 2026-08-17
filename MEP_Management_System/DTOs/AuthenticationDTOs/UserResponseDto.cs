using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.AuthenticationDTOs
{
    public class UserResponseDto
    {
        // Đổi từ string sang Guid để khớp với Database UUID
        public Guid UserId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? Token { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
    }
}