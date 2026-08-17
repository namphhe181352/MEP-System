using MEP_Management_System.DTOs.InventoryDTOs;

namespace MEP_Management_System.Services.InventoryService;

public interface IOutwardInventoryExcelService
{
    Task<OutwardExcelPreviewDto> ParseAndValidateAsync(
        Stream fileStream,
        string fileName,
        Guid branchId,
        Guid? targetBranchId,
        Guid userId);

    Task<OutwardExcelResultDto> CommitImportAsync(
        OutwardExcelCommitDto dto,
        Guid userId);
}
