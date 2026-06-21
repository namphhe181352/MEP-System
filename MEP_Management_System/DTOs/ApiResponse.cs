using System.Collections.Generic;

namespace MEP_Management_System.DTOs
{
    /// <summary>
    /// Wrapper chuẩn cho MỌI response trả về từ API.
    /// FE dựa vào <c>Success</c> + <c>Data</c> + <c>Message</c> + <c>Errors</c>
    /// để hiển thị banner / toast / form error.
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; } = true;
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Wrapper phân trang chuẩn cho các danh sách có Items + TotalCount.
    /// Dùng chung cho cả Inventory, Admin, ...
    /// </summary>
    public class PaginatedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
}
