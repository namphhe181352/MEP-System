using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.Models;

namespace MEP_Management_System.Seed;

/// <summary>
/// Insert đầy đủ data mẫu cho M.E.P Management System.
/// Được gọi lúc startup từ MEP.Auth.Api.Program.cs (sau khi EnsureCreated + Migrate).
/// Chỉ chạy khi DB trống để tránh duplicate key.
/// </summary>
public static class DatabaseSeeder
{
    private static readonly DateTime SeedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    public static async Task SeedAsync(MEPDatabaseContext context)
    {
        // Bỏ qua nếu DB đã có dữ liệu (chỉ seed lần đầu)
        if (await context.Users.AnyAsync())
        {
            return;
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("MEP@2026");

        // ============================================================
        // 1. USERS (12 users: 1 Admin + 2 Owner + 6 Staff + 3 Community)
        // ============================================================
        var users = new List<User>
        {
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000001"), Username = "admin@mep.vn", Email = "admin@mep.vn", FullName = "Nguyễn Văn Admin", PhoneNumber = "0901000001", PasswordHash = passwordHash, IsActive = 1, IsVerified = true, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000002"), Username = "owner.bac@mep.vn", Email = "owner.bac@mep.vn", FullName = "Trần Văn Bắc (Owner)", PhoneNumber = "0902000001", PasswordHash = passwordHash, IsActive = 1, IsVerified = true, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000003"), Username = "owner.nam@mep.vn", Email = "owner.nam@mep.vn", FullName = "Lê Thị Nam (Owner)", PhoneNumber = "0902000002", PasswordHash = passwordHash, IsActive = 1, IsVerified = true, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000004"), Username = "sale.bac01@mep.vn", Email = "sale.bac01@mep.vn", FullName = "Phạm Thị Hoa (Sale Bắc)", PhoneNumber = "0903000001", PasswordHash = passwordHash, IsActive = 1, IsVerified = true, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000005"), Username = "sale.bac02@mep.vn", Email = "sale.bac02@mep.vn", FullName = "Nguyễn Văn Minh (Sale Bắc)", PhoneNumber = "0903000002", PasswordHash = passwordHash, IsActive = 1, IsVerified = true, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000006"), Username = "sale.nam01@mep.vn", Email = "sale.nam01@mep.vn", FullName = "Đặng Thị Lan (Sale Nam)", PhoneNumber = "0903000003", PasswordHash = passwordHash, IsActive = 1, IsVerified = true, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000007"), Username = "kho.bac01@mep.vn", Email = "kho.bac01@mep.vn", FullName = "Hoàng Văn Tùng (Kho Bắc)", PhoneNumber = "0904000001", PasswordHash = passwordHash, IsActive = 1, IsVerified = true, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000008"), Username = "kho.nam01@mep.vn", Email = "kho.nam01@mep.vn", FullName = "Vũ Thị Hương (Kho Nam)", PhoneNumber = "0904000002", PasswordHash = passwordHash, IsActive = 1, IsVerified = true, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000009"), Username = "kho.bac02@mep.vn", Email = "kho.bac02@mep.vn", FullName = "Bùi Văn Cường (Kho Bắc)", PhoneNumber = "0904000003", PasswordHash = passwordHash, IsActive = 1, IsVerified = true, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-00000000000A"), Username = "community01@mep.vn", Email = "community01@mep.vn", FullName = "Lê Minh Tuấn (Community)", PhoneNumber = "0905000001", PasswordHash = passwordHash, IsActive = 1, IsVerified = true, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-00000000000B"), Username = "community02@mep.vn", Email = "community02@mep.vn", FullName = "Trần Thị Mai (Community)", PhoneNumber = "0905000002", PasswordHash = passwordHash, IsActive = 1, IsVerified = true, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-00000000000C"), Username = "community03@mep.vn", Email = "community03@mep.vn", FullName = "Phạm Văn Đức (Community)", PhoneNumber = "0905000003", PasswordHash = passwordHash, IsActive = 1, IsVerified = true, CreatedAt = SeedTime }
        };
        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();

        // ============================================================
        // 1.5. PERMISSIONS (tạo permissions cho hệ thống RBAC mới)
        // ============================================================
        var permissions = new List<Permission>
        {
            // SUPPLIER
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionCode = "SUPPLIER_VIEW", PermissionName = "Xem nhà cung cấp" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000002"), PermissionCode = "SUPPLIER_CREATE", PermissionName = "Tạo nhà cung cấp" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionCode = "SUPPLIER_UPDATE", PermissionName = "Cập nhật nhà cung cấp" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionCode = "SUPPLIER_DELETE", PermissionName = "Xóa nhà cung cấp" },
            // SUPPLIER PAYMENT
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000005"), PermissionCode = "SUPPLIER_PAYMENT_VIEW", PermissionName = "Xem thanh toán NCC" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000006"), PermissionCode = "SUPPLIER_PAYMENT_CREATE", PermissionName = "Tạo thanh toán NCC" },
            // PRODUCT
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000007"), PermissionCode = "PRODUCT_VIEW", PermissionName = "Xem sản phẩm" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000008"), PermissionCode = "PRODUCT_CREATE", PermissionName = "Tạo sản phẩm" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000009"), PermissionCode = "PRODUCT_UPDATE", PermissionName = "Cập nhật sản phẩm" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000010"), PermissionCode = "PRODUCT_DELETE", PermissionName = "Xóa sản phẩm" },
            // STOCK / INVENTORY
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000011"), PermissionCode = "STOCK_VIEW", PermissionName = "Xem tồn kho" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000012"), PermissionCode = "STOCK_INWARD_CREATE", PermissionName = "Tạo nhập kho" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000013"), PermissionCode = "STOCK_INWARD_UPDATE", PermissionName = "Cập nhật nhập kho" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000014"), PermissionCode = "STOCK_INWARD_DELETE", PermissionName = "Xóa nhập kho" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000015"), PermissionCode = "STOCK_OUTWARD_CREATE", PermissionName = "Tạo xuất kho" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000016"), PermissionCode = "STOCK_OUTWARD_UPDATE", PermissionName = "Cập nhật xuất kho" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000017"), PermissionCode = "STOCK_OUTWARD_DELETE", PermissionName = "Xóa xuất kho" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000018"), PermissionCode = "STOCK_CHECK_VIEW", PermissionName = "Xem kiểm kho" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000019"), PermissionCode = "STOCK_CHECK_CREATE", PermissionName = "Tạo kiểm kho" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000020"), PermissionCode = "STOCK_CHECK_APPROVE", PermissionName = "Duyệt kiểm kho" },
            // SALE / POS
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000021"), PermissionCode = "CUSTOMER_VIEW", PermissionName = "Xem khách hàng" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000022"), PermissionCode = "CUSTOMER_CREATE", PermissionName = "Tạo khách hàng" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000023"), PermissionCode = "CUSTOMER_UPDATE", PermissionName = "Cập nhật khách hàng" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000024"), PermissionCode = "CUSTOMER_DELETE", PermissionName = "Xóa khách hàng" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000025"), PermissionCode = "SALE_VIEW", PermissionName = "Xem bán hàng" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000026"), PermissionCode = "SALE_CREATE", PermissionName = "Tạo bán hàng" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000027"), PermissionCode = "SALE_UPDATE", PermissionName = "Cập nhật bán hàng" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000028"), PermissionCode = "SALE_DELETE", PermissionName = "Xóa bán hàng" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000030"), PermissionCode = "PAYMENT_VIEW", PermissionName = "Xem thanh toán" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000031"), PermissionCode = "PAYMENT_CREATE", PermissionName = "Tạo thanh toán" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000032"), PermissionCode = "PRINT_VIEW", PermissionName = "In hóa đơn" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000033"), PermissionCode = "PROMOTION_VIEW", PermissionName = "Xem khuyến mãi" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000034"), PermissionCode = "SHIFT_VIEW", PermissionName = "Xem ca làm việc" },
            // SYSTEM / OWNER
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000035"), PermissionCode = "OWNER_MANAGE", PermissionName = "Quản lý owner" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000036"), PermissionCode = "SYSTEM_MANAGE", PermissionName = "Quản lý hệ thống" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000037"), PermissionCode = "REPORT_VIEW", PermissionName = "Xem báo cáo" },
            // STAFF
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000038"), PermissionCode = "STAFF_VIEW", PermissionName = "Xem nhân viên" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000039"), PermissionCode = "STAFF_CREATE", PermissionName = "Tạo nhân viên" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000040"), PermissionCode = "STAFF_UPDATE", PermissionName = "Cập nhật nhân viên" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000041"), PermissionCode = "STAFF_DELETE", PermissionName = "Xóa nhân viên" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000042"), PermissionCode = "STAFF_ASSIGN_BRANCH", PermissionName = "Gán chi nhánh" },
            // EXPENSE / CHI PHÍ
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000043"), PermissionCode = "EXPENSE_VIEW", PermissionName = "Xem phiếu chi" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000044"), PermissionCode = "EXPENSE_CREATE", PermissionName = "Tạo phiếu chi" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000045"), PermissionCode = "EXPENSE_CONFIRM", PermissionName = "Xác nhận phiếu chi" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000046"), PermissionCode = "EXPENSE_CANCEL", PermissionName = "Hủy phiếu chi" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000047"), PermissionCode = "EXPENSE_CATEGORY_MANAGE", PermissionName = "Quản lý danh mục chi phí" },
            // SHIFT (CRUD)
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000060"), PermissionCode = "SHIFT_CREATE", PermissionName = "Mở ca làm việc" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000061"), PermissionCode = "SHIFT_UPDATE", PermissionName = "Chốt ca làm việc" },
            new() { PermissionId = Guid.Parse("00000000-0000-0000-0000-000000000062"), PermissionCode = "SHIFT_DELETE", PermissionName = "Hủy ca làm việc" },
        };
        await context.Permissions.AddRangeAsync(permissions);
        await context.SaveChangesAsync();

        // ============================================================
        // 1.6. BRANCHES (2 chi nhánh mẫu)
        // ============================================================
        var branchEntities = new List<Branch>
        {
            new()
            {
                BranchId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
                BranchName = "Chi nhánh Miền Bắc",
                BranchCode = "BAC",
                Phone = "02439990001",
                City = "Hà Nội",
                Address = "123 Cầu Giấy, Hà Nội",
                Type = "Cửa hàng",
                IsActive = 1,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime
            },
            new()
            {
                BranchId = Guid.Parse("30000000-0000-0000-0000-000000000002"),
                BranchName = "Chi nhánh Miền Nam",
                BranchCode = "NAM",
                Phone = "02839990002",
                City = "TP. Hồ Chí Minh",
                Address = "456 Quận 1, TP. Hồ Chí Minh",
                Type = "Cửa hàng",
                IsActive = 1,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime
            }
        };
        var existingBranchIds = await context.Branches
            .Where(b => branchEntities.Select(x => x.BranchId).Contains(b.BranchId))
            .Select(b => b.BranchId)
            .ToListAsync();
        var newBranches = branchEntities.Where(b => !existingBranchIds.Contains(b.BranchId)).ToList();
        if (newBranches.Any()) await context.Branches.AddRangeAsync(newBranches);
        await context.SaveChangesAsync();

        // ============================================================
        // 2. USER-ROLE MAPPING (gán role cho từng user)
        // ============================================================
        var userRoles = new[]
        {
            // Admin user
            new { UserId = Guid.Parse("20000000-0000-0000-0000-000000000001"), RoleId = Guid.Parse("10000000-0000-0000-0000-000000000001") },
            // 2 Owner users
            new { UserId = Guid.Parse("20000000-0000-0000-0000-000000000002"), RoleId = Guid.Parse("10000000-0000-0000-0000-000000000002") },
            new { UserId = Guid.Parse("20000000-0000-0000-0000-000000000003"), RoleId = Guid.Parse("10000000-0000-0000-0000-000000000002") },
            // 3 SalesStaff
            new { UserId = Guid.Parse("20000000-0000-0000-0000-000000000004"), RoleId = Guid.Parse("10000000-0000-0000-0000-000000000003") },
            new { UserId = Guid.Parse("20000000-0000-0000-0000-000000000005"), RoleId = Guid.Parse("10000000-0000-0000-0000-000000000003") },
            new { UserId = Guid.Parse("20000000-0000-0000-0000-000000000006"), RoleId = Guid.Parse("10000000-0000-0000-0000-000000000003") },
            // 3 InventoryStaff
            new { UserId = Guid.Parse("20000000-0000-0000-0000-000000000007"), RoleId = Guid.Parse("10000000-0000-0000-0000-000000000004") },
            new { UserId = Guid.Parse("20000000-0000-0000-0000-000000000008"), RoleId = Guid.Parse("10000000-0000-0000-0000-000000000004") },
            new { UserId = Guid.Parse("20000000-0000-0000-0000-000000000009"), RoleId = Guid.Parse("10000000-0000-0000-0000-000000000004") },
            // 3 CommunityUser
            new { UserId = Guid.Parse("20000000-0000-0000-0000-00000000000A"), RoleId = Guid.Parse("10000000-0000-0000-0000-000000000005") },
            new { UserId = Guid.Parse("20000000-0000-0000-0000-00000000000B"), RoleId = Guid.Parse("10000000-0000-0000-0000-000000000005") },
            new { UserId = Guid.Parse("20000000-0000-0000-0000-00000000000C"), RoleId = Guid.Parse("10000000-0000-0000-0000-000000000005") }
        };
        foreach (var ur in userRoles)
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO user_roles (user_id, role_id) VALUES ({0}, {1})",
                ur.UserId, ur.RoleId);
        }

        // ============================================================
        // 3. ROLE-PERMISSION MAPPING
        // Admin: tất cả 12 permission
        // Owner: tất cả trừ USER_MANAGE (do Admin quản)
        // SalesStaff: STOCK_VIEW, POS_SALE, RETURN_PROCESS, CUSTOMER_MANAGE, REPORT_VIEW
        // InventoryStaff: STOCK_VIEW, STOCK_MANAGE, STOCK_TRANSFER, PRODUCT_MANAGE, REPORT_VIEW
        // CommunityUser: STOCK_VIEW (chỉ xem giá)
        // ============================================================
        var rolePerms = new List<(Guid RoleId, Guid PermId)>
        {
            // Admin
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000001")),
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000002")),
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000003")),
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000004")),
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000005")),
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000006")),
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000007")),
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000008")),
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000009")),
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000010")),
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000011")),
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000012")),
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000034")), // SHIFT_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000060")), // SHIFT_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000061")), // SHIFT_UPDATE
            (Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000062")), // SHIFT_DELETE
            // Owner (không có USER_MANAGE - do Admin quản)
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000001")),
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000002")),
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000003")),
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000004")),
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000005")),
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000007")),
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000008")),
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000009")),
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000010")),
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000011")),
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000012")),
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000034")), // SHIFT_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000060")), // SHIFT_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000061")), // SHIFT_UPDATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000062")), // SHIFT_DELETE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000013")), // STOCK_INWARD_UPDATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000014")), // STOCK_INWARD_DELETE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000015")), // STOCK_OUTWARD_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000016")), // STOCK_OUTWARD_UPDATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000017")), // STOCK_OUTWARD_DELETE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000018")), // STOCK_CHECK_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000019")), // STOCK_CHECK_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000020")), // STOCK_CHECK_APPROVE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000021")), // CUSTOMER_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000022")), // CUSTOMER_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000023")), // CUSTOMER_UPDATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000024")), // CUSTOMER_DELETE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000025")), // SALE_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000026")), // SALE_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000027")), // SALE_UPDATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000028")), // SALE_DELETE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000030")), // PAYMENT_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000031")), // PAYMENT_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000032")), // PRINT_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000033")), // PROMOTION_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000035")), // OWNER_MANAGE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000036")), // SYSTEM_MANAGE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000037")), // REPORT_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000038")), // STAFF_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000039")), // STAFF_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000040")), // STAFF_UPDATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000041")), // STAFF_DELETE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000042")), // STAFF_ASSIGN_BRANCH
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000006")), // SUPPLIER_PAYMENT_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000043")), // EXPENSE_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000044")), // EXPENSE_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000045")), // EXPENSE_CONFIRM
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000046")), // EXPENSE_CANCEL
            (Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000047")), // EXPENSE_CATEGORY_MANAGE
            // SalesStaff - Sale permissions
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000001")), // STOCK_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000007")), // PRODUCT_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000021")), // CUSTOMER_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000022")), // CUSTOMER_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000025")), // SALE_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000026")), // SALE_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000030")), // PAYMENT_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000031")), // PAYMENT_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000032")), // PRINT_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000033")), // PROMOTION_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000034")), // SHIFT_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000060")), // SHIFT_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000061")), // SHIFT_UPDATE
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000062")), // SHIFT_DELETE
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000027")), // SALE_UPDATE
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000028")), // SALE_DELETE
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000023")), // CUSTOMER_UPDATE
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000024")), // CUSTOMER_DELETE
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000037")), // REPORT_VIEW
            // InventoryStaff
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000001")), // STOCK_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000002")), // SUPPLIER_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000007")), // PRODUCT_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000009")), // PRODUCT_UPDATE
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000010")), // PRODUCT_DELETE
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000011")), // STOCK_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000012")), // STOCK_INWARD_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000013")), // STOCK_INWARD_UPDATE
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000014")), // STOCK_INWARD_DELETE
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000015")), // STOCK_OUTWARD_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000016")), // STOCK_OUTWARD_UPDATE
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000017")), // STOCK_OUTWARD_DELETE
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000018")), // STOCK_CHECK_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000019")), // STOCK_CHECK_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000020")), // STOCK_CHECK_APPROVE
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000003")), // SUPPLIER_UPDATE
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000008")), // PRODUCT_CREATE
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000004")), // SUPPLIER_DELETE
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000005")), // SUPPLIER_PAYMENT_VIEW
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000006")), // SUPPLIER_PAYMENT_CREATE
            // CommunityUser
            (Guid.Parse("10000000-0000-0000-0000-000000000005"), Guid.Parse("00000000-0000-0000-0000-000000000001"))
        };
        foreach (var rp in rolePerms)
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO role_permissions (role_id, permission_id) VALUES ({0}, {1})",
                rp.RoleId, rp.PermId);
        }

        // ============================================================
        // 4. STAFF_ASSIGNMENTS (gán nhân viên vào chi nhánh)
        // ============================================================
        var assignments = new[]
        {
            new { AssignmentId = Guid.Parse("40000000-0000-0000-0000-000000000001"), UserId = Guid.Parse("20000000-0000-0000-0000-000000000002"), BranchId = Guid.Parse("30000000-0000-0000-0000-000000000001") }, // Owner Bắc -> Chi nhánh Bắc
            new { AssignmentId = Guid.Parse("40000000-0000-0000-0000-000000000002"), UserId = Guid.Parse("20000000-0000-0000-0000-000000000003"), BranchId = Guid.Parse("30000000-0000-0000-0000-000000000002") }, // Owner Nam -> Chi nhánh Nam
            new { AssignmentId = Guid.Parse("40000000-0000-0000-0000-000000000003"), UserId = Guid.Parse("20000000-0000-0000-0000-000000000004"), BranchId = Guid.Parse("30000000-0000-0000-0000-000000000001") }, // Sale Bắc 01
            new { AssignmentId = Guid.Parse("40000000-0000-0000-0000-000000000004"), UserId = Guid.Parse("20000000-0000-0000-0000-000000000005"), BranchId = Guid.Parse("30000000-0000-0000-0000-000000000001") }, // Sale Bắc 02
            new { AssignmentId = Guid.Parse("40000000-0000-0000-0000-000000000005"), UserId = Guid.Parse("20000000-0000-0000-0000-000000000006"), BranchId = Guid.Parse("30000000-0000-0000-0000-000000000002") }, // Sale Nam 01
            new { AssignmentId = Guid.Parse("40000000-0000-0000-0000-000000000006"), UserId = Guid.Parse("20000000-0000-0000-0000-000000000007"), BranchId = Guid.Parse("30000000-0000-0000-0000-000000000001") }, // Kho Bắc 01
            new { AssignmentId = Guid.Parse("40000000-0000-0000-0000-000000000007"), UserId = Guid.Parse("20000000-0000-0000-0000-000000000008"), BranchId = Guid.Parse("30000000-0000-0000-0000-000000000002") }, // Kho Nam 01
            new { AssignmentId = Guid.Parse("40000000-0000-0000-0000-000000000008"), UserId = Guid.Parse("20000000-0000-0000-0000-000000000009"), BranchId = Guid.Parse("30000000-0000-0000-0000-000000000001") },  // Kho Bắc 02
            new { AssignmentId = Guid.Parse("40000000-0000-0000-0000-000000000009"), UserId = Guid.Parse("20000000-0000-0000-0000-000000000001"), BranchId = Guid.Parse("30000000-0000-0000-0000-000000000001") }   // Admin -> Chi nhánh Bắc
        };
        foreach (var a in assignments)
        {
            await context.StaffAssignments.AddAsync(new StaffAssignment
            {
                AssignmentId = a.AssignmentId,
                UserId = a.UserId,
                BranchId = a.BranchId,
                AssignedAt = SeedTime
            });
        }
        await context.SaveChangesAsync();

        // ============================================================
        // 5. SUPPLIERS (6 NCC)
        // ============================================================
        var suppliers = new[]
        {
            new { Id = Guid.Parse("60000000-0000-0000-0000-000000000001"), Name = "Stanley Việt Nam", Phone = "02439991111", Email = "contact@stanley.vn", Address = "KCN Thăng Long, Hà Nội" },
            new { Id = Guid.Parse("60000000-0000-0000-0000-000000000002"), Name = "Panasonic Việt Nam", Phone = "02839992222", Email = "info@panasonic.vn", Address = "KCN Tân Thuận, TP.HCM" },
            new { Id = Guid.Parse("60000000-0000-0000-0000-000000000003"), Name = "Nhựa Bình Minh", Phone = "02839993333", Email = "sales@binhminhplastic.vn", Address = "KCN Vĩnh Lộc, TP.HCM" },
            new { Id = Guid.Parse("60000000-0000-0000-0000-000000000004"), Name = "Cadivi (Dây cáp điện)", Phone = "02839994444", Email = "cskh@cadivi.vn", Address = "KCN Tân Bình, TP.HCM" },
            new { Id = Guid.Parse("60000000-0000-0000-0000-000000000005"), Name = "Rạng Đông (Đèn LED)", Phone = "02439995555", Email = "info@rangdong.com.vn", Address = "KCN Hà Bình Phương, Hà Nội" },
            new { Id = Guid.Parse("60000000-0000-0000-0000-000000000006"), Name = "Tolsen Tools (Dụng cụ cầm tay)", Phone = "02439996666", Email = "sales@tolsen.vn", Address = "KCN Sài Đồng B, Hà Nội" }
        };
        var ownerDefaultId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        foreach (var s in suppliers)
        {
            await context.Suppliers.AddAsync(new Supplier
            {
                SupplierId = s.Id,
                SupplierName = s.Name,
                ContactPhone = s.Phone,
                ContactEmail = s.Email,
                Address = s.Address,
                Status = 1,
                CreatedAt = SeedTime,
                CreatedBy = ownerDefaultId
            });
        }
        await context.SaveChangesAsync();

        // ============================================================
        // 6. PRODUCTS (30 sản phẩm thuộc 5 categories, dùng cho demo)
        // ============================================================
        var products = new[]
        {
            // Kim Khí (Stanley, Tolsen)
            new { Code = "SP001", Name = "Búa sắt cán gỗ 500g", Unit = "Cái", Spec = "500g", Cat = "Kim Khí", Brand = "Stanley", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000001"), Cost = 85000m, Sell = 120000m, Min = 5m },
            new { Code = "SP002", Name = "Kìm cắt điện 8 inch", Unit = "Cái", Spec = "8 inch", Cat = "Kim Khí", Brand = "Tolsen", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000006"), Cost = 65000m, Sell = 95000m, Min = 5m },
            new { Code = "SP003", Name = "Mỏ lết 12 inch", Unit = "Cái", Spec = "12 inch", Cat = "Kim Khí", Brand = "Tolsen", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000006"), Cost = 95000m, Sell = 135000m, Min = 5m },
            new { Code = "SP004", Name = "Tua vít bake Stanley", Unit = "Cây", Spec = "Bake", Cat = "Kim Khí", Brand = "Stanley", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000001"), Cost = 25000m, Sell = 40000m, Min = 10m },
            new { Code = "SP005", Name = "Cờ lê vòng miệng 14mm", Unit = "Cây", Spec = "14mm", Cat = "Kim Khí", Brand = "Stanley", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000001"), Cost = 45000m, Sell = 70000m, Min = 10m },
            new { Code = "SP006", Name = "Kìm bấm rivê 10 inch", Unit = "Cái", Spec = "10 inch", Cat = "Kim Khí", Brand = "Tolsen", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000006"), Cost = 110000m, Sell = 165000m, Min = 3m },

            // Dụng Cụ Cầm Tay
            new { Code = "SP007", Name = "Thước kéo 5m", Unit = "Cái", Spec = "5 mét", Cat = "Dụng Cụ Cầm Tay", Brand = "Tolsen", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000006"), Cost = 55000m, Sell = 80000m, Min = 10m },
            new { Code = "SP008", Name = "Dao rọc giấy lớn 18mm", Unit = "Cái", Spec = "18mm", Cat = "Dụng Cụ Cầm Tay", Brand = "Stanley", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000001"), Cost = 12000m, Sell = 25000m, Min = 20m },
            new { Code = "SP009", Name = "Máy khoan mini 650W", Unit = "Cái", Spec = "650W", Cat = "Dụng Cụ Cầm Tay", Brand = "Stanley", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000001"), Cost = 650000m, Sell = 850000m, Min = 2m },
            new { Code = "SP010", Name = "Bộ mũi khoan 10 chi tiết", Unit = "Bộ", Spec = "10 chi tiết", Cat = "Dụng Cụ Cầm Tay", Brand = "Tolsen", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000006"), Cost = 95000m, Sell = 145000m, Min = 5m },

            // Điện Dân Dụng
            new { Code = "SP011", Name = "Bút thử điện 220V", Unit = "Cây", Spec = "220V", Cat = "Điện Dân Dụng", Brand = "Panasonic", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000002"), Cost = 8000m, Sell = 15000m, Min = 20m },
            new { Code = "SP012", Name = "Ổ cắm điện đôi 220V", Unit = "Cái", Spec = "220V", Cat = "Điện Dân Dụng", Brand = "Panasonic", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000002"), Cost = 45000m, Sell = 70000m, Min = 10m },
            new { Code = "SP013", Name = "Công tắc điện âm tường 1 chiều", Unit = "Cái", Spec = "1 chiều", Cat = "Điện Dân Dụng", Brand = "Panasonic", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000002"), Cost = 25000m, Sell = 45000m, Min = 20m },
            new { Code = "SP014", Name = "Dây điện Cadivi 2x1.5 (100m)", Unit = "Cuộn", Spec = "100m", Cat = "Điện Dân Dụng", Brand = "Cadivi", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000004"), Cost = 850000m, Sell = 1050000m, Min = 2m },
            new { Code = "SP015", Name = "CB 1 pha 20A Panasonic", Unit = "Cái", Spec = "20A", Cat = "Điện Dân Dụng", Brand = "Panasonic", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000002"), Cost = 65000m, Sell = 95000m, Min = 5m },
            new { Code = "SP016", Name = "Aptomat chống giật 30mA", Unit = "Cái", Spec = "30mA", Cat = "Điện Dân Dụng", Brand = "Panasonic", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000002"), Cost = 250000m, Sell = 380000m, Min = 3m },

            // Nước Dân Dụng
            new { Code = "SP017", Name = "Ống nước PVC 27mm", Unit = "Cây", Spec = "27mm x 4m", Cat = "Nước Dân Dụng", Brand = "Bình Minh", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000003"), Cost = 55000m, Sell = 75000m, Min = 10m },
            new { Code = "SP018", Name = "Ống nước PVC 34mm", Unit = "Cây", Spec = "34mm x 4m", Cat = "Nước Dân Dụng", Brand = "Bình Minh", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000003"), Cost = 70000m, Sell = 95000m, Min = 10m },
            new { Code = "SP019", Name = "Cút nối PVC 27mm", Unit = "Cái", Spec = "27mm", Cat = "Nước Dân Dụng", Brand = "Bình Minh", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000003"), Cost = 5000m, Sell = 10000m, Min = 30m },
            new { Code = "SP020", Name = "Vòi nước rửa bát 1 chân", Unit = "Cái", Spec = "inox 304", Cat = "Nước Dân Dụng", Brand = "Bình Minh", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000003"), Cost = 180000m, Sell = 280000m, Min = 3m },
            new { Code = "SP021", Name = "Bồn rửa chén inox 2 hộc", Unit = "Cái", Spec = "120cm", Cat = "Nước Dân Dụng", Brand = "Bình Minh", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000003"), Cost = 1850000m, Sell = 2500000m, Min = 1m },

            // Đèn Chiếu Sáng
            new { Code = "SP022", Name = "Bóng LED tròn 9W", Unit = "Bóng", Spec = "9W", Cat = "Đèn Chiếu Sáng", Brand = "Rạng Đông", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000005"), Cost = 25000m, Sell = 40000m, Min = 20m },
            new { Code = "SP023", Name = "Bóng LED tuýp 1m2 18W", Unit = "Bóng", Spec = "1.2m 18W", Cat = "Đèn Chiếu Sáng", Brand = "Rạng Đông", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000005"), Cost = 45000m, Sell = 70000m, Min = 15m },
            new { Code = "SP024", Name = "Đèn LED âm trần 12W", Unit = "Cái", Spec = "12W tròn", Cat = "Đèn Chiếu Sáng", Brand = "Rạng Đông", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000005"), Cost = 75000m, Sell = 120000m, Min = 8m },
            new { Code = "SP025", Name = "Đèn pha LED 50W", Unit = "Cái", Spec = "50W IP65", Cat = "Đèn Chiếu Sáng", Brand = "Rạng Đông", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000005"), Cost = 220000m, Sell = 320000m, Min = 3m },

            // Phụ kiện
            new { Code = "SP026", Name = "Băng keo điện Nano 18mm", Unit = "Cuộn", Spec = "18mm x 18m", Cat = "Điện Dân Dụng", Brand = "3M", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000004"), Cost = 12000m, Sell = 22000m, Min = 30m },
            new { Code = "SP027", Name = "Dây thít nhựa 200mm (100 sợi)", Unit = "Túi", Spec = "200mm", Cat = "Điện Dân Dụng", Brand = "3M", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000004"), Cost = 18000m, Sell = 30000m, Min = 20m },
            new { Code = "SP028", Name = "Keo dán ống PVC 100g", Unit = "Tuýp", Spec = "100g", Cat = "Nước Dân Dụng", Brand = "Bình Minh", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000003"), Cost = 28000m, Sell = 45000m, Min = 15m },
            new { Code = "SP029", Name = "Găng tay bảo hộ cao su", Unit = "Đôi", Spec = "Size L", Cat = "Dụng Cụ Cầm Tay", Brand = "Tolsen", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000006"), Cost = 15000m, Sell = 28000m, Min = 30m },
            new { Code = "SP030", Name = "Kính bảo hộ trong suốt", Unit = "Cái", Spec = "Chống bụi UV", Cat = "Dụng Cụ Cầm Tay", Brand = "Tolsen", Supplier = Guid.Parse("60000000-0000-0000-0000-000000000006"), Cost = 22000m, Sell = 38000m, Min = 20m }
        };

        var productEntities = new List<Product>();
        foreach (var p in products)
        {
            var product = new Product
            {
                ProductId = Guid.NewGuid(),
                ProductCode = p.Code,
                ProductName = p.Name,
                Barcode = $"8934{p.Code.Substring(2)}",
                BaseUnit = p.Unit,
                Specification = p.Spec,
                ItemType = p.Cat,
                BrandName = p.Brand,
                CategoryName = p.Cat,
                SupplierId = p.Supplier,
                SalesChannelLinked = 1,
                Status = 1,
                CreatedAt = SeedTime
            };
            productEntities.Add(product);
        }
        await context.Products.AddRangeAsync(productEntities);
        await context.SaveChangesAsync();

        // ============================================================
        // 7. BRANCH_PRODUCTS (stock cho mỗi sản phẩm tại mỗi chi nhánh)
        // ============================================================
        var branchProducts = new List<BranchProduct>();
        var branches = new[] { Guid.Parse("30000000-0000-0000-0000-000000000001"), Guid.Parse("30000000-0000-0000-0000-000000000002") };
        var index = 0;
        foreach (var product in productEntities)
        {
            var p = products[index++];
            foreach (var branchId in branches)
            {
                // Random stock từ 20-100 cho chi nhánh 1, 15-80 cho chi nhánh 2
                var baseStock = branchId == branches[0] ? 50 : 35;
                var stockQty = baseStock + (product.ProductCode.GetHashCode() % 30);
                branchProducts.Add(new BranchProduct
                {
                    BranchProductId = Guid.NewGuid(),
                    BranchId = branchId,
                    ProductId = product.ProductId,
                    CostPrice = p.Cost,
                    SellPrice = p.Sell,
                    ActualStock = stockQty,
                    AvailableStock = stockQty,
                    ReservedStock = 0,
                    MinimumStock = p.Min,
                    MaximumStock = p.Min * 20,
                    IsActive = 1,
                    DirectSale = 1,
                    ShelfLocation = $"Kệ {(char)('A' + Math.Abs(product.ProductCode.GetHashCode() % 6))}-{(index % 10) + 1}"
                });
            }
        }
        await context.BranchProducts.AddRangeAsync(branchProducts);
        await context.SaveChangesAsync();

        // ============================================================
        // 8. CUSTOMERS (10 khách hàng)
        // ============================================================
        var customers = new[]
        {
            new { Name = "Nguyễn Văn An", Phone = "0912000001", Email = "an.nv@gmail.com", Address = "12 Lý Thường Kiệt, Hà Nội", Loyalty = 250, DebtLimit = 5000000m, CurrentDebt = 0m },
            new { Name = "Trần Thị Bình", Phone = "0912000002", Email = "binh.tt@gmail.com", Address = "45 Nguyễn Huệ, TP.HCM", Loyalty = 480, DebtLimit = 8000000m, CurrentDebt = 1500000m },
            new { Name = "Lê Hoàng Cường", Phone = "0912000003", Email = "cuong.lh@yahoo.com", Address = "78 Trần Phú, Đà Nẵng", Loyalty = 100, DebtLimit = 3000000m, CurrentDebt = 0m },
            new { Name = "Phạm Thị Dung", Phone = "0912000004", Email = "dung.pt@gmail.com", Address = "23 Hai Bà Trưng, Hà Nội", Loyalty = 750, DebtLimit = 10000000m, CurrentDebt = 2000000m },
            new { Name = "Hoàng Văn Em", Phone = "0912000005", Email = "em.hv@gmail.com", Address = "67 Lê Lợi, TP.HCM", Loyalty = 0, DebtLimit = 2000000m, CurrentDebt = 0m },
            new { Name = "Vũ Thị Phương", Phone = "0912000006", Email = "phuong.vt@gmail.com", Address = "89 Phan Đình Phùng, Hà Nội", Loyalty = 1200, DebtLimit = 15000000m, CurrentDebt = 5000000m },
            new { Name = "Đặng Văn Giang", Phone = "0912000007", Email = "giang.dv@gmail.com", Address = "34 Bạch Đằng, Đà Nẵng", Loyalty = 350, DebtLimit = 5000000m, CurrentDebt = 0m },
            new { Name = "Bùi Thị Hoa", Phone = "0912000008", Email = "hoa.bt@gmail.com", Address = "56 Trưng Nữ Vương, TP.HCM", Loyalty = 90, DebtLimit = 3000000m, CurrentDebt = 0m },
            new { Name = "Công ty TNHH Xây dựng An Phát", Phone = "0912000009", Email = "info@anphat.com.vn", Address = "100 Cầu Giấy, Hà Nội", Loyalty = 5000, DebtLimit = 50000000m, CurrentDebt = 12000000m },
            new { Name = "Cửa hàng Điện nước Minh Tâm", Phone = "0912000010", Email = "minhtam.diennuoc@gmail.com", Address = "22 Lê Hồng Phong, TP.HCM", Loyalty = 3000, DebtLimit = 30000000m, CurrentDebt = 8000000m }
        };
        foreach (var c in customers)
        {
            await context.Customers.AddAsync(new Customer
            {
                CustomerId = Guid.NewGuid(),
                CustomerName = c.Name,
                PhoneNumber = c.Phone,
                Email = c.Email,
                Address = c.Address,
                LoyaltyPoints = c.Loyalty,
                DebtLimit = c.DebtLimit,
                CurrentDebt = c.CurrentDebt,
                Status = 1,
                CreatedAt = SeedTime
            });
        }
        await context.SaveChangesAsync();

        // ============================================================


        // ============================================================

        // ============================================================
        // 12. ORDERS + INVOICES (5 đơn hàng mẫu - test POS flow)
        // ============================================================
        var allCustomers = await context.Customers.ToListAsync();
        var allBranchProducts = await context.BranchProducts.Where(bp => bp.BranchId == Guid.Parse("30000000-0000-0000-0000-000000000001")).Take(5).ToListAsync();
        var saleUser1 = Guid.Parse("20000000-0000-0000-0000-000000000004");
        var saleUser2 = Guid.Parse("20000000-0000-0000-0000-000000000006");

        for (int i = 0; i < 5; i++)
        {
            var orderId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var cust = allCustomers[i];
            var bp = allBranchProducts[i];
            var saleUser = i % 2 == 0 ? saleUser1 : saleUser2;
            var qty = (decimal)(i + 1) * 2;
            var total = qty * bp.SellPrice;
            var tax = total * 0.1m;
            var grandTotal = total + tax;

            await context.Orders.AddAsync(new Order
            {
                OrderId = orderId,
                OrderCode = $"ORD-2026-{i + 1:D4}",
                BranchId = bp.BranchId,
                CustomerId = cust.CustomerId,
                UserId = saleUser,
                TotalAmount = grandTotal,
                DiscountAmount = 0m,
                TaxAmount = tax,
                PaymentMethod = i == 0 ? "CASH" : (i == 1 ? "TRANSFER" : (i == 2 ? "DEBT" : "CASH")),
                Status = "COMPLETED",
                Note = i == 0 ? "Khách quen, giao trong ngày" : null,
                CreatedAt = SeedTime.AddDays(-i * 2)
            });
            await context.OrderItems.AddAsync(new OrderItem
            {
                OrderItemId = Guid.NewGuid(),
                OrderId = orderId,
                BranchProductId = bp.BranchProductId,
                Quantity = qty,
                SellPrice = bp.SellPrice
            });
            await context.Invoices.AddAsync(new Invoice
            {
                InvoiceId = invoiceId,
                InvoiceCode = $"INV-2026-{i + 1:D4}",
                OrderId = orderId,
                UserId = saleUser,
                TotalAmount = grandTotal,
                PaymentMethod = i == 0 ? "CASH" : (i == 1 ? "TRANSFER" : (i == 2 ? "DEBT" : "CASH")),
                Status = "COMPLETED",
                Note = null,
                CreatedAt = SeedTime.AddDays(-i * 2).AddMinutes(5)
            });
            await context.InvoiceItems.AddAsync(new InvoiceItem
            {
                InvoiceItemId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                BranchProductId = bp.BranchProductId,
                Quantity = qty,
                SellPrice = bp.SellPrice
            });
        }
        await context.SaveChangesAsync();

        // ============================================================
        // 13. PURCHASE ORDERS (3 PO mẫu)
        // ============================================================
        var khoUser1 = Guid.Parse("20000000-0000-0000-0000-000000000007");
        var khoUser2 = Guid.Parse("20000000-0000-0000-0000-000000000008");
        var ownerBac = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var ownerNam = Guid.Parse("20000000-0000-0000-0000-000000000003");

        for (int i = 0; i < 3; i++)
        {
            var poId = Guid.NewGuid();
            var khoUser = i % 2 == 0 ? khoUser1 : khoUser2;
            var approver = i % 2 == 0 ? ownerBac : ownerNam;
            var supplierId = Guid.Parse($"60000000-0000-0000-0000-00000000000{i + 1}");
            var sampleBps = await context.BranchProducts.Where(bp => bp.BranchId == (i % 2 == 0 ? Guid.Parse("30000000-0000-0000-0000-000000000001") : Guid.Parse("30000000-0000-0000-0000-000000000002"))).Take(2).ToListAsync();
            var totalAmount = sampleBps.Sum(bp => bp.CostPrice * 10);

            await context.PurchaseOrders.AddAsync(new PurchaseOrder
            {
                PurchaseOrderId = poId,
                OrderCode = $"PO-2026-{i + 1:D4}",
                SupplierId = supplierId,
                UserId = khoUser,
                TotalAmount = totalAmount,
                Status = i == 0 ? "PENDING" : (i == 1 ? "APPROVED" : "COMPLETED"),
                ApprovedBy = i == 0 ? (Guid?)null : approver,
                ApprovedAt = i == 0 ? (DateTime?)null : SeedTime.AddDays(-i * 3),
                ExpectedDeliveryDate = SeedTime.AddDays(7 - i * 2),
                Notes = i == 0 ? "Đơn nhập hàng Kim Khí đầu quý" : null,
                CreatedAt = SeedTime.AddDays(-i * 3 - 1)
            });
            foreach (var bp in sampleBps)
            {
                await context.PurchaseOrderItems.AddAsync(new PurchaseOrderItem
                {
                    PoItemId = Guid.NewGuid(),
                    PurchaseOrderId = poId,
                    ProductId = bp.ProductId,
                    Quantity = 10m,
                    UnitPrice = bp.CostPrice
                });
            }
        }
        await context.SaveChangesAsync();

        // ============================================================
        // 14. STOCK TICKETS (1 phiếu kiểm kê mẫu)
        // ============================================================
        var stockTicketId = Guid.NewGuid();
        await context.StockTickets.AddAsync(new StockTicket
        {
            StockTicketId = stockTicketId,
            TicketCode = "STK-2026-0001",
            BranchId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            UserId = khoUser1,
            TicketType = "STOCK_CHECK",
            Status = "PENDING",
            Notes = "Kiểm kê quý 1/2026",
            CreatedAt = SeedTime
        });
        // Thêm 3 dòng kiểm kê
        var checkBps = await context.BranchProducts.Where(bp => bp.BranchId == Guid.Parse("30000000-0000-0000-0000-000000000001")).Take(3).ToListAsync();
        foreach (var bp in checkBps)
        {
            await context.StockTicketItems.AddAsync(new StockTicketItem
            {
                TicketItemId = Guid.NewGuid(),
                StockTicketId = stockTicketId,
                BranchProductId = bp.BranchProductId,
                SystemQuantity = bp.ActualStock,
                ActualQuantity = bp.ActualStock - 1, // mất 1 đơn vị
                Discrepancy = -1m
            });
        }
        await context.SaveChangesAsync();


        // ============================================================
        // 16. SYSTEM LOGS (5 log mẫu)
        // ============================================================
        await context.SystemLogs.AddRangeAsync(
            new SystemLog { LogId = Guid.NewGuid(), UserId = ownerBac, Action = "APPROVE_PO", TableName = "purchase_orders", EntityType = "PurchaseOrder", Description = "Duyệt đơn mua hàng PO-2026-0002", IpAddress = "192.168.1.10", UserAgent = "Chrome/120.0", Level = "INFO", Source = "InventoryService", Timestamp = SeedTime.AddDays(-2) },
            new SystemLog { LogId = Guid.NewGuid(), UserId = saleUser1, Action = "CREATE_INVOICE", TableName = "invoices", EntityType = "Invoice", Description = "Tạo hóa đơn INV-2026-0001 - 320,000 VND", IpAddress = "192.168.1.20", UserAgent = "Chrome/120.0", Level = "INFO", Source = "SaleService", Timestamp = SeedTime.AddDays(-1) },
            new SystemLog { LogId = Guid.NewGuid(), UserId = khoUser1, Action = "CREATE_STOCK_TICKET", TableName = "stock_tickets", EntityType = "StockTicket", Description = "Tạo phiếu kiểm kê STK-2026-0001", IpAddress = "192.168.1.30", UserAgent = "Firefox/121.0", Level = "INFO", Source = "InventoryService", Timestamp = SeedTime },
            new SystemLog { LogId = Guid.NewGuid(), UserId = Guid.Parse("20000000-0000-0000-0000-000000000001"), Action = "RESOLVE_VIOLATION", TableName = "violation_reports", EntityType = "ViolationReport", Description = "Xử lý báo cáo vi phạm #1 - ẩn bài viết", IpAddress = "192.168.1.5", UserAgent = "Edge/120.0", Level = "WARN", Source = "AdminService", Timestamp = SeedTime.AddDays(-2) },
            new SystemLog { LogId = Guid.NewGuid(), UserId = ownerNam, Action = "CREATE_BRANCH_PRODUCT", TableName = "branch_products", EntityType = "BranchProduct", Description = "Thêm sản phẩm mới vào chi nhánh Nam", IpAddress = "192.168.2.10", UserAgent = "Safari/17.1", Level = "INFO", Source = "InventoryService", Timestamp = SeedTime.AddDays(-1).AddHours(8) }
        );
        await context.SaveChangesAsync();

        // ============================================================
        // 17. SYSTEM NOTIFICATIONS (5 notification mẫu)
        // ============================================================
        var adminId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        await context.SystemNotifications.AddRangeAsync(
            new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                Title = "Chào mừng đến với M.E.P System",
                Content = "Cảm ơn quý cửa hàng đã tham gia hệ thống M.E.P Management. Chúc quý cửa hàng kinh doanh thắng lợi!",
                Target = "ALL",
                IsUrgent = false,
                Status = "SENT",
                SentAt = SeedTime.AddDays(-10),
                CreatedBy = adminId,
                CreatedAt = SeedTime.AddDays(-10)
            },
            new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                Title = "Bảo trì hệ thống ngày 15/06/2026",
                Content = "Hệ thống sẽ bảo trì từ 02:00 đến 04:00 sáng. Vui lòng lưu dữ liệu trước giờ bảo trì.",
                Target = "ALL",
                IsUrgent = true,
                Status = "SENT",
                SentAt = SeedTime.AddDays(-3),
                CreatedBy = adminId,
                CreatedAt = SeedTime.AddDays(-3)
            },
            new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                Title = "Thông báo cập nhật chính sách giá",
                Content = "Từ ngày 20/06/2026, chính sách giá bán lẻ sẽ được cập nhật theo bảng giá mới.",
                Target = "OWNER",
                IsUrgent = false,
                Status = "SCHEDULED",
                ScheduledAt = SeedTime.AddDays(7),
                CreatedBy = adminId,
                CreatedAt = SeedTime.AddDays(-1)
            },
            new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                Title = "Cập nhật quy định vận chuyển",
                Content = "Thông tin về quy định vận chuyển mới cho các sản phẩm có giá trị cao.",
                Target = "STAFF",
                IsUrgent = false,
                Status = "SENT",
                SentAt = SeedTime.AddDays(-5),
                CreatedBy = adminId,
                CreatedAt = SeedTime.AddDays(-5)
            },
            new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                Title = "Hướng dẫn sử dụng module Báo cáo",
                Content = "Video hướng dẫn sử dụng module Báo cáo doanh thu đã được cập nhật.",
                Target = "OWNER",
                IsUrgent = false,
                Status = "DRAFT",
                CreatedBy = adminId,
                CreatedAt = SeedTime
            }
        );
        await context.SaveChangesAsync();



        // Update existing customers with Group, Notes, TotalSpent, OrderCount, LastVisit, MembershipTier, UpdatedAt
        var existingCustomers = context.Customers.ToList();
        var customerGroups = new[] { "Individual", "Enterprise", "Agent", "Contractor" };
        var tiers = new[] { "Bronze", "Silver", "Gold", "Platinum" };
        var rand = new Random(42);
        for (int i = 0; i < allCustomers.Count; i++)
        {
            var c = existingCustomers[i];
            c.Group = customerGroups[i % customerGroups.Length];
            c.Notes = i == 0 ? "Khách hàng VIP, ưu tiên xử lý" : (i == 1 ? "Cần theo dõi công nợ" : null);
            c.TotalSpent = (decimal)(rand.NextDouble() * 50000000 + 100000);
            c.OrderCount = rand.Next(1, 50);
            c.LastVisit = DateTime.UtcNow.AddDays(-rand.Next(1, 60));
            c.MembershipTier = tiers[rand.Next(0, tiers.Length)];
            c.UpdatedAt = DateTime.UtcNow;
        }

        // ============================================================
        // 20. SHIFTS (Sprint 1 - Sale API)
        // ============================================================
        var hnBranchId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var saleUserId = Guid.Parse("20000000-0000-0000-0000-000000000004"); // sale.bac01

        var shifts = new List<Shift>
        {
            new Shift
            {
                ShiftId = Guid.Parse("e0000000-0000-0000-0000-000000000001"),
                ShiftCode = "SHIFT-20260621-001",
                UserId = saleUserId,
                BranchId = hnBranchId,
                Status = "OPEN",
                StartedAt = DateTime.UtcNow.Date.AddHours(8),
                OpeningBalance = 5000000m,
                ExpectedCash = null,
                ActualCash = null,
                Variance = null,
                TotalRevenue = 0m,
                TotalOrders = 0
            },
            new Shift
            {
                ShiftId = Guid.Parse("e0000000-0000-0000-0000-000000000002"),
                ShiftCode = "SHIFT-20260620-001",
                UserId = saleUserId,
                BranchId = hnBranchId,
                Status = "CLOSED",
                StartedAt = DateTime.UtcNow.Date.AddDays(-1).AddHours(8),
                EndedAt = DateTime.UtcNow.Date.AddDays(-1).AddHours(17),
                OpeningBalance = 5000000m,
                ExpectedCash = 8750000m,
                ActualCash = 8720000m,
                Variance = -30000m,
                TotalRevenue = 3750000m,
                TotalOrders = 12
            }
        };
        context.Shifts.AddRange(shifts);

        // ============================================================
        // 9. EXPENSE CATEGORIES (Danh mục chi phí mẫu)
        // ============================================================
        var expenseCategories = new List<ExpenseCategory>
        {
            new() { CategoryId = Guid.Parse("50000000-0000-0000-0000-000000000001"), CategoryName = "Điện nước", IsActive = 1, CreatedAt = SeedTime },
            new() { CategoryId = Guid.Parse("50000000-0000-0000-0000-000000000002"), CategoryName = "Văn phòng phẩm", IsActive = 1, CreatedAt = SeedTime },
            new() { CategoryId = Guid.Parse("50000000-0000-0000-0000-000000000003"), CategoryName = "Vệ sinh", IsActive = 1, CreatedAt = SeedTime },
            new() { CategoryId = Guid.Parse("50000000-0000-0000-0000-000000000004"), CategoryName = "Bảo trì sửa chữa", IsActive = 1, CreatedAt = SeedTime },
            new() { CategoryId = Guid.Parse("50000000-0000-0000-0000-000000000005"), CategoryName = "Marketing", IsActive = 1, CreatedAt = SeedTime },
            new() { CategoryId = Guid.Parse("50000000-0000-0000-0000-000000000006"), CategoryName = "Chi phí khác", IsActive = 1, CreatedAt = SeedTime },
        };
        context.ExpenseCategories.AddRange(expenseCategories);

        // ============================================================
        if (existingCustomers.Count > 0)
        {
        }

        await context.SaveChangesAsync();
    }
}






