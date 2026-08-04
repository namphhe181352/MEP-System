using System.Security.Claims;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Middleware;

/// <summary>
/// Middleware chặn request khi tài khoản đã bị vô hiệu hóa / xóa mềm sau khi đã phát hành JWT.
/// Áp dụng cho tất cả các route trừ /api/auth/*, /swagger, /health, /r/* (SignalR) và static files.
/// Cache DB check 30s để giảm tải.
/// </summary>
public class ActiveUserMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ActiveUserMiddleware> _logger;
    private static readonly Dictionary<Guid, CacheEntry> _cache = new();
    private static readonly object _cacheLock = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private record CacheEntry(bool IsActive, string? Status, DateTime ExpiresAt);

    public ActiveUserMiddleware(RequestDelegate next, ILogger<ActiveUserMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, MEPDatabaseContext db)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Bỏ qua các path không cần kiểm tra
        if (ShouldSkip(path))
        {
            await _next(context);
            return;
        }

        // Chỉ kiểm tra khi request đã xác thực (có NameIdentifier claim)
        var userIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            await _next(context);
            return;
        }

        // Kiểm tra trạng thái từ cache trước
        var entry = GetFromCache(userId);

        // Hết hạn hoặc chưa có → query DB
        if (entry == null)
        {
            try
            {
                var user = await db.Users
                    .AsNoTracking()
                    .Where(u => u.UserId == userId)
                    .Select(u => new { u.IsActive, u.Status })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    // User đã bị xóa cứng — chặn
                    await RejectAsync(context, "Tài khoản không tồn tại. Vui lòng đăng nhập lại.");
                    return;
                }

                var isActive = (user.IsActive ?? 0) == 1;
                entry = new CacheEntry(isActive, user.Status, DateTime.UtcNow.Add(CacheTtl));
                SetCache(userId, entry);
            }
            catch (Exception ex)
            {
                // Không block request nếu DB lỗi — log và cho qua (fail-open)
                _logger.LogWarning(ex, "ActiveUserMiddleware không thể truy vấn DB, cho qua request");
                await _next(context);
                return;
            }
        }

        if (entry == null || !entry.IsActive || string.Equals(entry.Status, "DELETED", StringComparison.OrdinalIgnoreCase))
        {
            // Xóa cache để lần tới re-check (phòng admin khôi phục)
            InvalidateCache(userId);
            await RejectAsync(context, "Tài khoản đã bị khóa hoặc xóa. Vui lòng đăng nhập lại.");
            return;
        }

        await _next(context);
    }

    private static bool ShouldSkip(string path)
    {
        return path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/r/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task RejectAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            $"{{\"success\":false,\"message\":\"{message}\",\"code\":\"ACCOUNT_INACTIVE\"}}");
    }

    private static CacheEntry? GetFromCache(Guid userId)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(userId, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
            {
                return entry;
            }
            return null;
        }
    }

    private static void SetCache(Guid userId, CacheEntry entry)
    {
        lock (_cacheLock)
        {
            _cache[userId] = entry;
        }
    }

    private static void InvalidateCache(Guid userId)
    {
        lock (_cacheLock)
        {
            _cache.Remove(userId);
        }
    }
}

public static class ActiveUserMiddlewareExtensions
{
    public static IApplicationBuilder UseActiveUserCheck(this IApplicationBuilder app)
        => app.UseMiddleware<ActiveUserMiddleware>();
}
