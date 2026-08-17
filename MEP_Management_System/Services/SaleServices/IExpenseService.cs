using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;

namespace MEP_Management_System.Services.SaleServices;

public interface IExpenseService
{
    Task<PaginatedResponse<ExpenseVoucherResponseDto>> GetVouchersAsync(Guid branchId, ExpenseVoucherParameters @params);
    Task<ExpenseVoucherResponseDto?> GetVoucherByIdAsync(Guid id, Guid branchId);
    Task<ExpenseVoucherResponseDto> CreateVoucherAsync(ExpenseVoucherCreateDto dto, Guid userId, Guid branchId);
    Task<ExpenseVoucherResponseDto?> UpdateVoucherAsync(Guid id, UpdateExpenseVoucherDto dto, Guid userId, bool isOwner);
    Task<ExpenseVoucherResponseDto?> ConfirmVoucherAsync(Guid id, Guid userId, Guid branchId);
    Task<ExpenseVoucherResponseDto?> CancelVoucherAsync(Guid id, CancelExpenseVoucherDto dto, Guid userId, Guid branchId);
    Task<List<ExpenseCategoryDto>> GetCategoriesAsync(Guid branchId);
    Task<ExpenseCategoryDto> CreateCategoryAsync(ExpenseCategoryUpsertDto dto, Guid userId, Guid branchId);
    Task<ExpenseCategoryDto?> UpdateCategoryAsync(Guid id, ExpenseCategoryUpsertDto dto, Guid userId, Guid branchId);
    Task<bool> DeleteCategoryAsync(Guid id, Guid userId, Guid branchId);
}
