using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.AccountSettings_DTOs
{
    /// <summary>
    /// Response DTO cho GET /api/account-settings/me
    /// </summary>
    public class AccountProfileDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string? Status { get; set; }
        public DateTime? SuspendedUntil { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<string> Roles { get; set; } = new();
    }
}
