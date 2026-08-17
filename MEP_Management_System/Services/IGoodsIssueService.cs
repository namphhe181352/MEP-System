using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.DTOs.SaleDTOs;

namespace MEP_Management_System.Services;

/// <summary>
/// Service cho Phiếu xuất kho nội bộ (Goods Issue), khớp contract FE module Xuất Kho.
/// </summary>
public interface IGoodsIssueService
{
    Task<ApiResponse<PaginatedResponse<GoodsIssueResponseDto>>> GetGoodsIssuesAsync(
        ClaimsPrincipal user, Guid? branchId, string? keyword, string? status,
        string? issueType, DateTime? fromDate, DateTime? toDate, int page, int pageSize);

    Task<ApiResponse<GoodsIssueResponseDto>> GetGoodsIssueByIdAsync(
        ClaimsPrincipal user, Guid id, Guid? branchId);

    Task<ApiResponse<object>> CreateGoodsIssueAsync(
        ClaimsPrincipal user, GoodsIssueCreateDto dto, Guid? branchId);

    Task<ApiResponse<GoodsIssueResponseDto>> UpdateGoodsIssueAsync(
        ClaimsPrincipal user, Guid id, GoodsIssueCreateDto dto, Guid? branchId);

    Task<ApiResponse<object>> DeleteGoodsIssueAsync(
        ClaimsPrincipal user, Guid id, Guid? branchId);

    Task<ApiResponse<List<GoodsIssueSearchProductDto>>> SearchProductsAsync(
        ClaimsPrincipal user, Guid? branchId, string? keyword);

    Task<ApiResponse<List<GoodsIssueCustomerDto>>> GetCustomersAsync(
        ClaimsPrincipal user, Guid? branchId, string? search);

    Task<ApiResponse<GoodsIssueCustomerDto>> CreateCustomerAsync(
        ClaimsPrincipal user, Guid? branchId, CreateCustomerRequest request);

    Task<ApiResponse<List<GoodsIssueWarehouseDto>>> GetWarehousesAsync(
        ClaimsPrincipal user, Guid? branchId);

    Task<ApiResponse<GoodsIssueAttachmentResponseDto>> UploadAttachmentAsync(
        ClaimsPrincipal user, Guid? branchId, Guid id, IFormFile file);
}