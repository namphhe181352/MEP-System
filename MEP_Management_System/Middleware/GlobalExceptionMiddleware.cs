using System;
using System.Text.Json;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.Exceptions;
using Microsoft.AspNetCore.Http;

namespace MEP_Management_System.Middleware;

/// <summary>
/// Middleware bắt tất cả exceptions và chuyển thành API responses chuẩn.
/// - BusinessException → HTTP 400 Bad Request
/// - KeyNotFoundException → HTTP 404 Not Found
/// - UnauthorizedAccessException → HTTP 401 Unauthorized
/// - Exception → HTTP 500 Internal Server Error
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var apiResponse = new ApiResponse<object>();

        switch (exception)
        {
            case BusinessException businessEx:
                response.StatusCode = 400;
                apiResponse.Success = false;
                apiResponse.Message = businessEx.Message;
                apiResponse.Errors = businessEx.Errors.Count > 0 
                    ? businessEx.Errors 
                    : new() { businessEx.Message };
                _logger.LogWarning("BusinessException: {Message}", businessEx.Message);
                break;

            case KeyNotFoundException notFoundEx:
                response.StatusCode = 404;
                apiResponse.Success = false;
                apiResponse.Message = notFoundEx.Message;
                apiResponse.Errors = new() { notFoundEx.Message };
                _logger.LogWarning("KeyNotFoundException: {Message}", notFoundEx.Message);
                break;

            case UnauthorizedAccessException unauthorizedEx:
                response.StatusCode = 401;
                apiResponse.Success = false;
                apiResponse.Message = unauthorizedEx.Message;
                apiResponse.Errors = new() { "Unauthorized access." };
                _logger.LogWarning("UnauthorizedAccessException: {Message}", unauthorizedEx.Message);
                break;

            case ArgumentException argEx:
                response.StatusCode = 400;
                apiResponse.Success = false;
                apiResponse.Message = argEx.Message;
                apiResponse.Errors = new() { argEx.Message };
                _logger.LogWarning("ArgumentException: {Message}", argEx.Message);
                break;

            default:
                response.StatusCode = 500;
                apiResponse.Success = false;
                apiResponse.Message = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.";
                apiResponse.Errors = new() { exception.Message };
                _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
                break;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var result = JsonSerializer.Serialize(apiResponse, options);
        await response.WriteAsync(result);
    }
}

/// <summary>
/// Extension method để đăng ký middleware vào pipeline.
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
