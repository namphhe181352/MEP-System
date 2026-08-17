using System;
using System.Collections.Generic;

namespace MEP_Management_System.Exceptions;

/// <summary>
/// Exception dùng cho lỗi nghiệp vụ (Business Logic Errors).
/// Khi throw BusinessException, GlobalExceptionMiddleware sẽ trả về HTTP 400 Bad Request.
/// </summary>
public class BusinessException : Exception
{
    public int StatusCode => 400;

    public BusinessException(string message) : base(message)
    {
        Errors = new List<string>();
    }

    public BusinessException(string message, List<string> errors) : base(message)
    {
        Errors = errors ?? new List<string>();
    }

    public BusinessException(string message, string error) : base(message)
    {
        Errors = new List<string> { error };
    }

    /// <summary>
    /// Danh sách các lỗi chi tiết (nếu có nhiều validation errors).
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
