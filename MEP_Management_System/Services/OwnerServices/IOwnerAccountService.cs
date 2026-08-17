using MEP_Management_System.DTOs;

namespace MEP_Management_System.Services.OwnerServices;

public interface IOwnerAccountService
{
    Task<PageResultDto<StaffAccountDto>> GetOwnersAsync(string? search, string? status, int page, int pageSize);
    Task<StaffAccountDto?> GetStaffByIdAsync(Guid id);
    Task<Guid> CreateOwnerAsync(CreateOwnerRequest request);
    Task UpdateStaffAsync(Guid id, UpdateOwnerRequest request);
    Task UpdateStatusAsync(Guid id, string status);
    Task BanStaffAsync(Guid id, string reason, Guid? adminId);
}
