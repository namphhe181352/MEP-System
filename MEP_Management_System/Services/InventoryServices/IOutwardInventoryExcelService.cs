using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using Microsoft.AspNetCore.Http;

namespace MEP_Management_System.Services.InventoryServices;

/// <summary>
/// Service interface for Excel-based outward inventory operations.
/// </summary>
public interface IOutwardInventoryExcelService
{
    Task<ApiResponse<OutwardExcelPreviewDto>> ParseAndValidateExcelAsync(IFormFile file, Guid branchId, Guid userId, Guid? targetBranchId);
    Task<ApiResponse<OutwardExcelResultDto>> CommitImportAsync(OutwardExcelCommitDto dto, Guid branchId, Guid userId);
    Task ResolveBranchIdAsync(Guid? requestedBranchId, Guid userId, Guid? targetBranchId);
}
