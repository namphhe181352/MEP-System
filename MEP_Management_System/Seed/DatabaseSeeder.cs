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
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000001"), Username = "admin@mep.vn", Email = "admin@mep.vn", FullName = "Nguyễn Văn Admin", PhoneNumber = "0901000001", PasswordHash = passwordHash, IsActive = 1, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000002"), Username = "owner.bac@mep.vn", Email = "owner.bac@mep.vn", FullName = "Trần Văn Bắc (Owner)", PhoneNumber = "0902000001", PasswordHash = passwordHash, IsActive = 1, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000003"), Username = "owner.nam@mep.vn", Email = "owner.nam@mep.vn", FullName = "Lê Thị Nam (Owner)", PhoneNumber = "0902000002", PasswordHash = passwordHash, IsActive = 1, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000004"), Username = "sale.bac01@mep.vn", Email = "sale.bac01@mep.vn", FullName = "Phạm Thị Hoa (Sale Bắc)", PhoneNumber = "0903000001", PasswordHash = passwordHash, IsActive = 1, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000005"), Username = "sale.bac02@mep.vn", Email = "sale.bac02@mep.vn", FullName = "Nguyễn Văn Minh (Sale Bắc)", PhoneNumber = "0903000002", PasswordHash = passwordHash, IsActive = 1, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000006"), Username = "sale.nam01@mep.vn", Email = "sale.nam01@mep.vn", FullName = "Đặng Thị Lan (Sale Nam)", PhoneNumber = "0903000003", PasswordHash = passwordHash, IsActive = 1, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000007"), Username = "kho.bac01@mep.vn", Email = "kho.bac01@mep.vn", FullName = "Hoàng Văn Tùng (Kho Bắc)", PhoneNumber = "0904000001", PasswordHash = passwordHash, IsActive = 1, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000008"), Username = "kho.nam01@mep.vn", Email = "kho.nam01@mep.vn", FullName = "Vũ Thị Hương (Kho Nam)", PhoneNumber = "0904000002", PasswordHash = passwordHash, IsActive = 1, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-000000000009"), Username = "kho.bac02@mep.vn", Email = "kho.bac02@mep.vn", FullName = "Bùi Văn Cường (Kho Bắc)", PhoneNumber = "0904000003", PasswordHash = passwordHash, IsActive = 1, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-00000000000A"), Username = "community01@mep.vn", Email = "community01@mep.vn", FullName = "Lê Minh Tuấn (Community)", PhoneNumber = "0905000001", PasswordHash = passwordHash, IsActive = 1, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-00000000000B"), Username = "community02@mep.vn", Email = "community02@mep.vn", FullName = "Trần Thị Mai (Community)", PhoneNumber = "0905000002", PasswordHash = passwordHash, IsActive = 1, CreatedAt = SeedTime },
            new() { UserId = Guid.Parse("20000000-0000-0000-0000-00000000000C"), Username = "community03@mep.vn", Email = "community03@mep.vn", FullName = "Phạm Văn Đức (Community)", PhoneNumber = "0905000003", PasswordHash = passwordHash, IsActive = 1, CreatedAt = SeedTime }
        };
        await context.Users.AddRangeAsync(users);
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
            // SalesStaff
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000001")),
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000003")),
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000009")),
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000012")),
            (Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000008")),
            // InventoryStaff
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000001")),
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000002")),
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000010")),
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000011")),
            (Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("00000000-0000-0000-0000-000000000008")),
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
                CreatedAt = SeedTime
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
        // 9. COMMUNITY CATEGORIES (5 chuyên mục diễn đàn)
        // ============================================================
        var communityCategories = new[]
        {
            new { Name = "Chia sẻ nguồn hàng Kim Khí", Desc = "Nơi các đại lý chia sẻ nguồn cung ứng kim khí, sắt thép" },
            new { Name = "Thiết bị Điện & Chiếu sáng", Desc = "Trao đổi về thiết bị điện, đèn LED, dây cáp" },
            new { Name = "Ống nước & Phụ kiện", Desc = "Chia sẻ thông tin về ống nước, phụ kiện PVC, vòi sen" },
            new { Name = "Dụng cụ cầm tay", Desc = "Bàn luận về máy khoan, tua vít, thước kéo" },
            new { Name = "Gom đơn (Group Buying)", Desc = "Tổ chức gom đơn hàng loạt để có giá tốt từ NCC" }
        };
        var categoryEntities = new List<CommunityCategory>();
        foreach (var cat in communityCategories)
        {
            var entity = new CommunityCategory
            {
                CategoryId = Guid.NewGuid(),
                CategoryName = cat.Name,
                Description = cat.Desc
            };
            categoryEntities.Add(entity);
            await context.CommunityCategories.AddAsync(entity);
        }
        await context.SaveChangesAsync();

        // ============================================================
        // 10. COMMUNITY POSTS (8 bài viết mẫu)
        // ============================================================
        var posts = new (string Title, string Content, Guid AuthorId, int CategoryIndex, short IsPinned, string Status, int ViewCount, bool IsGroupBuying, int? GbTarget, decimal? GbPrice)[]
        {
            ("[Hà Nội] Cần tìm nguồn thép cuộn CB240 giá tốt", "Mình đang cần mua 50 tấn thép cuộn CB240, ai có nguồn giá tốt inbox nhé. Yêu cầu có CO/CQ đầy đủ.", Guid.Parse("20000000-0000-0000-0000-00000000000A"), 0, (short)1, "ACTIVE", 245, true, 50, 14500000m),
            ("Chia sẻ: Kinh nghiệm chọn máy khoan cho thợ bán chuyên", "Sau 5 năm dùng máy khoan, mình thấy Bosch và Stanley là 2 dòng đáng tin nhất. Bài viết chia sẻ chi tiết...", Guid.Parse("20000000-0000-0000-0000-00000000000B"), 3, (short)0, "ACTIVE", 89, false, null, null),
            ("[Group Buying] Gom đơn dây điện Cadivi 2x1.5 - 100 cuộn", "Tổ chức gom 100 cuộn dây Cadivi 2x1.5 giá 1.020.000đ/cuộn. Đóng tiền trước 30%. Đạt mục tiêu 100 cuộn sẽ chốt đơn.", Guid.Parse("20000000-0000-0000-0000-00000000000C"), 4, (short)1, "ACTIVE", 412, true, 100, 1020000m),
            ("Cảnh báo: Ống PVC Bình Minh giả tràn lan trên thị trường", "Mình vừa mua phải ống PVC giả, bao bì in nhái logo Bình Minh. Mọi người chú ý chỉ mua ở đại lý chính hãng...", Guid.Parse("20000000-0000-0000-0000-00000000000A"), 2, (short)0, "ACTIVE", 678, false, null, null),
            ("Review đèn LED Rạng Đông 9W - đáng đồng tiền?", "Mua về test thực tế 1 tháng, so sánh với Điện Quang và Panasonic. Kết quả khá bất ngờ...", Guid.Parse("20000000-0000-0000-0000-00000000000B"), 1, (short)0, "ACTIVE", 156, false, null, null),
            ("[Đã chốt] Tìm được nguồn vòi nước inox 304 giá xưởng", "Update: đã chốt đơn 200 vòi với giá 195.000đ/cái từ nhà máy Bình Dương. Cảm ơn anh em đã hỗ trợ!", Guid.Parse("20000000-0000-0000-0000-00000000000C"), 2, (short)0, "LOCKED", 234, false, null, null),
            ("Hỏi: Aptomat chống giật loại nào tốt cho gia đình?", "Mình đang phân vân giữa Panasonic và Schneider. Anh em tư vấn giúp với.", Guid.Parse("20000000-0000-0000-0000-00000000000A"), 1, (short)0, "ACTIVE", 92, false, null, null),
            ("[HCM] Bán thanh lý 50 cuộn dây điện Cadivi tồn kho", "Cửa hàng mình chuyển mặt hàng, cần thanh lý 50 cuộn dây điện Cadivi còn mới 95%. Giá 880.000đ/cuộn.", Guid.Parse("20000000-0000-0000-0000-00000000000B"), 1, (short)0, "HIDDEN", 18, false, null, null)
        };
        foreach (var p in posts)
        {
            await context.CommunityPosts.AddAsync(new CommunityPost
            {
                PostId = Guid.NewGuid(),
                CategoryId = categoryEntities[p.CategoryIndex].CategoryId,
                UserId = p.AuthorId,
                Title = p.Title,
                Content = p.Content,
                Status = p.Status,
                IsPinned = p.IsPinned,
                ViewCount = p.ViewCount,
                GroupBuyingTarget = p.GbTarget,
                GroupBuyingPrice = p.GbPrice,
                CreatedAt = SeedTime.AddDays(-new Random(p.Title.GetHashCode()).Next(1, 30))
            });
        }
        await context.SaveChangesAsync();

        // ============================================================
        // 11. COMMENTS (15 comments mẫu)
        // ============================================================
        var firstPost = await context.CommunityPosts.OrderBy(p => p.CreatedAt).FirstAsync();
        var secondPost = await context.CommunityPosts.OrderBy(p => p.CreatedAt).Skip(1).FirstAsync();
        var comments = new[]
        {
            new { PostId = firstPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000B"), Text = "Mình có nguồn Hòa Phát, inbox mình nhé." },
            new { PostId = firstPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000C"), Text = "Đang có 30 tấn CB240 tại kho Bình Dương, giá 14.6tr/tấn." },
            new { PostId = firstPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000A"), Text = "Cảm ơn 2 bạn, mình inbox chi tiết." },
            new { PostId = firstPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000B"), Text = "Lưu ý: thép Hòa Phát phải có tem chính hãng nhé bạn." },
            new { PostId = secondPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000A"), Text = "Bài viết rất chi tiết, mình đang phân vân mua máy khoan." },
            new { PostId = secondPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000C"), Text = "Mình dùng Bosch 5 năm rồi, rất bền." },
            new { PostId = secondPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000B"), Text = "Stanley giá mềm hơn mà vẫn ok." },
            new { PostId = secondPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000A"), Text = "Cảm ơn bạn, mình quyết định mua Bosch GBM 13RE." },
            new { PostId = firstPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000C"), Text = "Update: đã chốt được nguồn 14.4tr/tấn từ Formosa." },
            new { PostId = secondPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000B"), Text = "Pin lithium hay pin đây bạn?" },
            new { PostId = firstPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000A"), Text = "Rep bạn: thép cuộn, có CO/CQ nhé." },
            new { PostId = secondPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000C"), Text = "Pin đây 12V, 2 cục pin + sạc." },
            new { PostId = firstPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000B"), Text = "Bạn mua xong nhớ review cho anh em nhé." },
            new { PostId = secondPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000A"), Text = "Pin 2 cụp thì xài thoải mái rồi." },
            new { PostId = firstPost.PostId, UserId = Guid.Parse("20000000-0000-0000-0000-00000000000C"), Text = "Đóng deal thành công rồi anh em ơi, cảm ơn forum!" }
        };
        foreach (var c in comments)
        {
            await context.Comments.AddAsync(new Comment
            {
                CommentId = Guid.NewGuid(),
                PostId = c.PostId,
                UserId = c.UserId,
                CommentText = c.Text,
                CreatedAt = SeedTime.AddDays(-new Random(c.Text.GetHashCode()).Next(1, 25))
            });
        }
        await context.SaveChangesAsync();

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
        // 15. VIOLATION REPORTS (2 report mẫu)
        // ============================================================
        var hiddenPost = await context.CommunityPosts.Where(p => p.Status == "HIDDEN").FirstOrDefaultAsync();
        if (hiddenPost != null)
        {
            await context.ViolationReports.AddAsync(new ViolationReport
            {
                ReportId = Guid.NewGuid(),
                PostId = hiddenPost.PostId,
                UserId = Guid.Parse("20000000-0000-0000-0000-00000000000A"),
                Reason = "Spam quảng cáo sản phẩm không liên quan",
                Status = "RESOLVED",
                ResolvedBy = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                ResolvedAt = SeedTime.AddDays(-2),
                AdminNote = "Đã ẩn bài viết và cảnh cáo tác giả",
                CreatedAt = SeedTime.AddDays(-3)
            });
        }
        // 1 report PENDING
        var firstActivePost = await context.CommunityPosts.Where(p => p.Status == "ACTIVE").OrderBy(p => p.CreatedAt).FirstAsync();
        await context.ViolationReports.AddAsync(new ViolationReport
        {
            ReportId = Guid.NewGuid(),
            PostId = firstActivePost.PostId,
            UserId = Guid.Parse("20000000-0000-0000-0000-00000000000B"),
            Reason = "Giá ảo, có dấu hiệu phá giá thị trường",
            Status = "PENDING",
            ResolvedBy = null,
            ResolvedAt = null,
            AdminNote = null,
            CreatedAt = SeedTime.AddDays(-1)
        });
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

        // ============================================================
        // 18. STORE APPROVALS (5 store approval mẫu)
        // ============================================================
        // Create some pending owner registrations
        var communityUsers = await context.Users
            .Include(u => u.Roles)
            .Where(u => u.Roles.Any(r => r.RoleName == "CommunityUser"))
            .Take(3)
            .ToListAsync();

        await context.StoreApprovals.AddRangeAsync(
            new StoreApproval
            {
                ApprovalId = Guid.NewGuid(),
                UserId = communityUsers.ElementAtOrDefault(0)?.UserId ?? Guid.NewGuid(),
                StoreName = "Vật Tư Điện Bắc Ninh",
                BusinessLicense = "BN-00123456",
                TaxCode = "2400123456",
                StoreAddress = "123 Đường Trần Hưng Đạo, TP Bắc Ninh, Bắc Ninh",
                DocumentUrls = "[]",
                Status = "PENDING",
                CreatedAt = SeedTime.AddDays(-3)
            },
            new StoreApproval
            {
                ApprovalId = Guid.NewGuid(),
                UserId = communityUsers.ElementAtOrDefault(1)?.UserId ?? Guid.NewGuid(),
                StoreName = "Thiết Bị Điện Miền Trung",
                BusinessLicense = "HN-00876543",
                TaxCode = "0108123456",
                StoreAddress = "45 Lê Lợi, TP Đà Nẵng",
                DocumentUrls = "[]",
                Status = "PENDING",
                CreatedAt = SeedTime.AddDays(-2)
            },
            new StoreApproval
            {
                ApprovalId = Guid.NewGuid(),
                UserId = communityUsers.ElementAtOrDefault(2)?.UserId ?? Guid.NewGuid(),
                StoreName = "Cấp Nước Sạch Sài Gòn",
                BusinessLicense = "HCM-00112233",
                TaxCode = "0312456789",
                StoreAddress = "78 Nguyễn Trãi, Quận 5, TP.HCM",
                DocumentUrls = "[]",
                Status = "PENDING",
                CreatedAt = SeedTime.AddDays(-1)
            },
            new StoreApproval
            {
                ApprovalId = Guid.NewGuid(),
                UserId = ownerBac,
                StoreName = "Vật Tư Xây Dựng Trần Bắc",
                BusinessLicense = "HN-00123457",
                TaxCode = "0101122334",
                StoreAddress = "12 Hoàng Quốc Việt, Cầu Giấy, Hà Nội",
                DocumentUrls = "[]",
                Status = "APPROVED",
                ReviewedBy = adminId,
                ReviewedAt = SeedTime.AddDays(-5),
                CreatedAt = SeedTime.AddDays(-7)
            },
            new StoreApproval
            {
                ApprovalId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                StoreName = "Điện Lạnh Gia Lai",
                BusinessLicense = "GL-00000001",
                TaxCode = "5901234567",
                StoreAddress = "90 Lê Duẩn, TP Pleiku, Gia Lai",
                DocumentUrls = "[]",
                Status = "REJECTED",
                RejectReason = "Giấy phép kinh doanh không hợp lệ. Vui lòng tải lên giấy phép mới.",
                ReviewedBy = adminId,
                ReviewedAt = SeedTime.AddDays(-4),
                CreatedAt = SeedTime.AddDays(-6)
            }
        );
        await context.SaveChangesAsync();

        // ============================================================
        // 19. PROMOTIONS (Sprint 1 - Sale API)
        // ============================================================
        var promotions = new List<Promotion>
        {
            new Promotion
            {
                PromotionId = Guid.Parse("d0000000-0000-0000-0000-000000000001"),
                PromotionCode = "SUMMER2026",
                Name = "Summer Sale 2026",
                Description = "Giảm 10% cho đơn hàng từ 500.000đ trở lên",
                DiscountType = "Percentage",
                DiscountValue = 10m,
                MinOrderAmount = 500000m,
                MaxDiscountAmount = 100000m,
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                UsageLimit = 1000,
                UsedCount = 0,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1)
            },
            new Promotion
            {
                PromotionId = Guid.Parse("d0000000-0000-0000-0000-000000000002"),
                PromotionCode = "NEWUSER",
                Name = "Khuyến mãi khách hàng mới",
                Description = "Giảm 50.000đ cho khách hàng lần đầu mua hàng",
                DiscountType = "FixedAmount",
                DiscountValue = 50000m,
                MinOrderAmount = 200000m,
                MaxDiscountAmount = 50000m,
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                UsageLimit = 500,
                UsedCount = 0,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1)
            },
            new Promotion
            {
                PromotionId = Guid.Parse("d0000000-0000-0000-0000-000000000003"),
                PromotionCode = "VIP10",
                Name = "VIP 10% Off",
                Description = "Giảm 10% không giới hạn cho khách VIP",
                DiscountType = "Percentage",
                DiscountValue = 10m,
                MinOrderAmount = 0m,
                MaxDiscountAmount = null,
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                UsageLimit = null,
                UsedCount = 0,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1)
            }
        };
        context.Promotions.AddRange(promotions);

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
        // 21. LOYALTY TRANSACTIONS (Sprint 1 - Sale API)
        // ============================================================
        if (existingCustomers.Count > 0)
        {
            var loyaltyTxns = new List<LoyaltyTransaction>();
            for (int i = 0; i < Math.Min(5, existingCustomers.Count); i++)
            {
                var c = existingCustomers[i];
                loyaltyTxns.Add(new LoyaltyTransaction
                {
                    TransactionId = Guid.Parse("f0000000-0000-0000-0000-00000000000" + (i + 1)),
                    CustomerId = c.CustomerId,
                    Points = (i + 1) * 100,
                    TransactionType = "EARN",
                    ReferenceType = "Order",
                    ReferenceId = Guid.NewGuid(),
                    Description = "Tích điểm từ mua hàng",
                    CreatedAt = DateTime.UtcNow.AddDays(-i * 3)
                });
            }
            context.LoyaltyTransactions.AddRange(loyaltyTxns);
        }

        await context.SaveChangesAsync();
    }
}
