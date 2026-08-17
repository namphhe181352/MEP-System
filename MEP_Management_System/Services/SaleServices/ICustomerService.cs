using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.SaleDTOs;

namespace MEP_Management_System.Services.SaleServices;

/// <summary>
/// Interface cho CustomerService.
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// Lấy danh sách khách hàng có phân trang.
    /// </summary>
    Task<(List<CustomerDto> Items, int Total)> GetCustomersAsync(Guid branchId, string? search, string? group, int page, int pageSize);

    /// <summary>
    /// Lấy tất cả khách hàng (không phân trang).
    /// </summary>
    Task<List<CustomerDto>> GetAllCustomersAsync(Guid branchId, string? search);

    /// <summary>
    /// Lấy thông tin 1 khách hàng.
    /// </summary>
    Task<CustomerDto?> GetCustomerAsync(Guid customerId, Guid branchId);

    /// <summary>
    /// Tạo khách hàng mới.
    /// </summary>
    Task<(CustomerDto? Customer, string? ErrorCode, string? ErrorMessage)> CreateCustomerAsync(
        Guid branchId, Guid createdBy, CreateCustomerRequest request);

    /// <summary>
    /// Cập nhật khách hàng.
    /// </summary>
    Task<(CustomerDto? Customer, string? ErrorCode, string? ErrorMessage)> UpdateCustomerAsync(
        Guid customerId, Guid branchId, UpdateCustomerRequest request);

    /// <summary>
    /// Xóa khách hàng (soft/hard delete).
    /// </summary>
    Task<(bool Success, string? ErrorCode, string? ErrorMessage, bool SoftDelete)> DeleteCustomerAsync(
        Guid customerId, Guid branchId);

    /// <summary>
    /// Lấy lịch sử đơn hàng của khách hàng.
    /// </summary>
    Task<(List<CustomerOrderDto> Items, int Total)> GetCustomerOrdersAsync(Guid customerId, Guid branchId, int page, int pageSize);
}
