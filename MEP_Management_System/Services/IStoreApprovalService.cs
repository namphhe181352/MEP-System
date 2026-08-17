using System;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;

namespace MEP_Management_System.Services;

public interface IStoreApprovalService
{
    Task<PageResultDto<StoreApprovalDto>> GetApprovalsAsync(string? status, string? search, int page, int pageSize);
    Task<StoreApprovalDto> GetApprovalByIdAsync(Guid id);
    Task<StoreApprovalDto> ApproveAsync(Guid id, ApproveStoreRequest dto, Guid adminId);
    Task<StoreApprovalDto> RejectAsync(Guid id, RejectStoreRequest dto, Guid adminId);
}