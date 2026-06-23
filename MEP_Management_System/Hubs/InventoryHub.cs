using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MEP_Management_System.Hubs;

[Authorize] // Bắt buộc phải có Token hợp lệ mới cho phép kết nối Socket
public class InventoryHub : Hub
{
    // Hàm này tự động chạy khi một Client (FE) thiết lập kết nối thành công đến Hub
    public override async Task OnConnectedAsync()
    {
        var branchId = GetUserBranchId();

        if (!string.IsNullOrEmpty(branchId))
        {
            // Tự động đưa User này vào một "Nhóm" riêng của Chi nhánh đó
            // Ví dụ tên nhóm: "Branch_3fa85f64-5717..."
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Branch_{branchId}");
        }

        await base.OnConnectedAsync();
    }

    // Hàm này tự động chạy khi Client ngắt kết nối (tắt trình duyệt, chuyển trang...)
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var branchId = GetUserBranchId();

        if (!string.IsNullOrEmpty(branchId))
        {
            // Đưa User ra khỏi nhóm khi họ rời đi
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Branch_{branchId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Hàm phụ trợ lấy BranchId từ Token (Kế thừa giống cách bạn lấy UserId ở Controller)
    private string? GetUserBranchId()
    {
        // Bạn có thể sửa lại tên Claim ("BranchId" hoặc "branch") cho đúng với cấu trúc Token hiện tại của bạn
        return Context.User?.FindFirst("BranchId")?.Value
            ?? Context.User?.FindFirst("branch")?.Value;
    }
}