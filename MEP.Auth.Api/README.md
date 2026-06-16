# 🔐 MEP Auth API

**Authentication & Authorization Service** cho hệ thống quản lý chuỗi cửa hàng M.E.P.

> Service này xử lý toàn bộ luồng xác thực: đăng ký doanh nghiệp (Owner), đăng nhập, và quản lý tài khoản nhân viên (Staff).

---

## 📋 Mục lục

- [Yêu cầu hệ thống](#-yêu-cầu-hệ-thống)
- [Cài đặt & Chạy](#-cài-đặt--chạy)
- [Cấu hình](#-cấu-hình)
- [API Endpoints](#-api-endpoints)
- [Luồng hoạt động](#-luồng-hoạt-động)
- [Cấu trúc thư mục](#-cấu-trúc-thư-mục)

---

## 💻 Yêu cầu hệ thống

| Thành phần | Phiên bản |
|---|---|
| .NET SDK | 8.0 trở lên |
| PostgreSQL | 14+ |
| IDE (khuyên dùng) | Visual Studio 2022 / Rider / VS Code |

---

## 🚀 Cài đặt & Chạy

### 1. Clone repository

```bash
git clone <repo-url>
cd MEP-System
```

### 2. Cấu hình Database & JWT

Mở file `MEP.Auth.Api/appsettings.json` và cập nhật connection string cùng JWT settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<DB_HOST>;Port=5432;Database=<DB_NAME>;Username=<DB_USER>;Password=<DB_PASSWORD>"
  },
  "Jwt": {
    "Key": "<YOUR_SECRET_KEY_BASE64>",
    "Issuer": "InventoryServiceAPI",
    "Audience": "InventoryServiceUsers",
    "ExpiryInMinutes": 60
  }
}
```

> ⚠️ **Lưu ý:** `Jwt:Key` phải là chuỗi Base64 đủ dài (ít nhất 256-bit) để đảm bảo bảo mật.

### 3. Chạy project

```bash
cd MEP.Auth.Api
dotnet restore
dotnet run
```

Sau khi chạy thành công, truy cập Swagger UI tại:
- **HTTP:** http://localhost:5100/swagger
- **HTTPS:** https://localhost:7176/swagger

> Database sẽ tự động migrate và seed dữ liệu mẫu khi khởi động lần đầu.

---

## ⚙️ Cấu hình

### Connection String

| Tham số | Mô tả |
|---|---|
| `Host` | Địa chỉ server PostgreSQL |
| `Port` | Cổng PostgreSQL (mặc định `5432`) |
| `Database` | Tên database |
| `Username` | Tên đăng nhập PostgreSQL |
| `Password` | Mật khẩu PostgreSQL |

### JWT Settings

| Tham số | Mô tả |
|---|---|
| `Key` | Secret key mã hoá token (Base64) |
| `Issuer` | Tên service phát hành token |
| `Audience` | Đối tượng sử dụng token |
| `ExpiryInMinutes` | Thời gian sống của token (phút) |

---

## 📡 API Endpoints

Base URL: `http://localhost:5100/api/Auth`

### 1. Đăng ký — Bước 1: Gửi OTP

```
POST /api/Auth/register/start
```

Kiểm tra email chưa được đăng ký, sau đó tạo mã OTP (6 số) có hiệu lực **10 phút**.

**Request Body:**

```json
{
  "email": "owner@example.com"
}
```

**Response (200 OK):**

```json
{
  "message": "Mã xác thực OTP đã được tạo thành công. Vui lòng kiểm tra email của bạn.",
  "otp": "123456"
}
```

**Lỗi có thể gặp:**

| Status | Trường hợp |
|---|---|
| `400` | Email đã được đăng ký |

---

### 2. Đăng ký — Bước 2: Xác thực OTP & Tạo tài khoản Owner

```
POST /api/Auth/register/verify
```

Xác thực mã OTP, sau đó tạo đồng thời: **tài khoản Owner** + **chi nhánh (Branch)** + **phân công nhân viên (StaffAssignment)**.

**Request Body:**

```json
{
  "email": "owner@example.com",
  "otpCode": "123456",
  "password": "matKhauManh123",
  "fullName": "Nguyễn Văn A",
  "branchName": "Cửa hàng Quận 1"
}
```

**Response (200 OK):**

```json
{
  "message": "Đăng ký doanh nghiệp và tài khoản quản trị tối cao thành công!",
  "branchId": "uuid-of-branch",
  "roles": ["Owner"],
  "data": {
    "userId": "uuid-of-user",
    "username": "owner@example.com",
    "fullName": "Nguyễn Văn A",
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "roles": ["Owner"]
  }
}
```

**Validation:**

| Trường | Quy tắc |
|---|---|
| `email` | Bắt buộc, đúng định dạng email |
| `otpCode` | Bắt buộc, đúng 6 ký tự |
| `password` | Bắt buộc, tối thiểu 6 ký tự |
| `fullName` | Bắt buộc |
| `branchName` | Bắt buộc |

**Lỗi có thể gặp:**

| Status | Trường hợp |
|---|---|
| `400` | OTP sai hoặc đã hết hạn |

---

### 3. Đăng nhập

```
POST /api/Auth/login
```

Dùng chung cho cả **Owner** và **Staff**. Trả về JWT Token kèm danh sách vai trò và mã chi nhánh.

**Request Body:**

```json
{
  "email": "owner@example.com",
  "password": "matKhauManh123"
}
```

**Response (200 OK):**

```json
{
  "message": "Đăng nhập thành công!",
  "branchId": "uuid-of-branch",
  "roles": ["Owner"],
  "data": {
    "userId": "uuid-of-user",
    "username": "owner@example.com",
    "fullName": "Nguyễn Văn A",
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "roles": ["Owner"]
  }
}
```

**Lỗi có thể gặp:**

| Status | Trường hợp |
|---|---|
| `400` | Email chưa được đăng ký |
| `400` | Mật khẩu không chính xác |
| `400` | Tài khoản bị khóa (`IsActive != 1`) |
| `400` | Chưa được phân công vào chi nhánh nào |

---

### 4. Tạo nhân viên (Owner only)

```
POST /api/Auth/create-staff
```

> 🔒 **Yêu cầu xác thực:** Bearer Token với role `Owner`

Owner tạo tài khoản nhân viên, nhân viên tự động được gán vào **cùng chi nhánh** của Owner.

**Headers:**

```
Authorization: Bearer <JWT_TOKEN>
```

**Request Body:**

```json
{
  "fullName": "Trần Thị B",
  "email": "staff@example.com",
  "password": "matKhau123"
}
```

**Response (200 OK):**

```json
{
  "message": "Đã tạo thành công nhân viên Trần Thị B thuộc chi nhánh của bạn!"
}
```

**Validation:**

| Trường | Quy tắc |
|---|---|
| `fullName` | Bắt buộc, tối đa 100 ký tự |
| `email` | Bắt buộc, đúng định dạng email |
| `password` | Bắt buộc, 6–50 ký tự |

**Lỗi có thể gặp:**

| Status | Trường hợp |
|---|---|
| `401` | Không có token hoặc không phải Owner |
| `400` | Email nhân viên đã được sử dụng |

---

## 🔄 Luồng hoạt động

### Đăng ký doanh nghiệp mới (Owner)

```
Client                          Auth API                         Database
  │                                │                                │
  │── POST /register/start ───────>│                                │
  │   { email }                    │── Kiểm tra email trùng ───────>│
  │                                │<── Không trùng ────────────────│
  │                                │── Tạo OTP (6 số, 10 phút) ───>│
  │<── 200 { otp } ───────────────│                                │
  │                                │                                │
  │── POST /register/verify ──────>│                                │
  │   { email, otp, password,      │── Xác thực OTP ──────────────>│
  │     fullName, branchName }     │<── OTP hợp lệ ────────────────│
  │                                │── Tạo Role Owner ────────────>│
  │                                │── Tạo Branch ────────────────>│
  │                                │── Tạo User (hash password) ──>│
  │                                │── Tạo StaffAssignment ───────>│
  │                                │── Sinh JWT Token              │
  │<── 200 { token, branchId } ───│                                │
```

### Đăng nhập

```
Client                          Auth API                         Database
  │                                │                                │
  │── POST /login ────────────────>│                                │
  │   { email, password }          │── Tìm user theo email ───────>│
  │                                │<── Trả về user ───────────────│
  │                                │── Verify BCrypt password      │
  │                                │── Lấy danh sách roles ───────>│
  │                                │── Lấy StaffAssignment ───────>│
  │                                │── Sinh JWT Token (roles +     │
  │                                │   branchId trong claims)      │
  │<── 200 { token, roles,         │                                │
  │         branchId } ───────────│                                │
```

---

## 📂 Cấu trúc thư mục

```
MEP.Auth.Api/
├── Controllers/
│   └── AuthController.cs          # Controller chính xử lý authentication
├── DTOs/
│   └── Authentication_DTOs/
│       ├── RegisterStartDto.cs     # DTO bước 1 đăng ký (gửi email)
│       ├── RegisterVerifyDto.cs    # DTO bước 2 đăng ký (xác thực OTP)
│       ├── LoginDto.cs             # DTO đăng nhập
│       ├── CreateStaffDto.cs       # DTO tạo nhân viên
│       ├── ForgotPasswordDto.cs    # DTO quên mật khẩu (chưa triển khai)
│       └── UserResponseDto.cs     # DTO response chung cho user
├── AutoMapper/
│   └── MappingProfile.cs          # Cấu hình mapping Entity <-> DTO
├── Formatters/
│   └── CustomAuthOutputFormatter.cs # Custom formatter cho response
├── Properties/
│   └── launchSettings.json        # Cấu hình launch (port, environment)
├── Program.cs                     # Entry point & cấu hình services
├── appsettings.json               # Cấu hình chính (DB, JWT)
├── appsettings.Development.json   # Cấu hình cho môi trường Development
└── MEP.Auth.Api.csproj            # File project (.NET 8)
```

---

## 🔑 JWT Token Claims

Token được sinh ra chứa các thông tin sau:

| Claim | Mô tả |
|---|---|
| `sub` | UserId |
| `unique_name` | Username |
| `name` | FullName |
| `role` | Danh sách vai trò (Owner / Staff) |
| `branchId` | Mã chi nhánh được phân công |

---

## 📦 Dependencies

| Package | Mục đích |
|---|---|
| `MEP.Data` | Entity Framework models & DbContext |
| `MEP.Shared` | Services dùng chung (JwtHelper,...) |
| `Microsoft.EntityFrameworkCore.Design` | Hỗ trợ EF migrations |
| `BCrypt.Net` | Hash & verify mật khẩu |
| `AutoMapper` | Mapping Entity ↔ DTO |
| `Microsoft.AspNetCore.OData` | Hỗ trợ query OData |

---

## 🧪 Test nhanh với Swagger

1. Chạy project: `dotnet run`
2. Mở trình duyệt: http://localhost:5100/swagger
3. Thử lần lượt các API theo thứ tự:
   - `POST /api/Auth/register/start` → Lấy OTP
   - `POST /api/Auth/register/verify` → Đăng ký & nhận Token
   - `POST /api/Auth/login` → Đăng nhập
4. Để test API `create-staff`:
   - Click nút **Authorize** 🔓 trên Swagger
   - Nhập: `Bearer <token_từ_bước_trên>`
   - Gọi `POST /api/Auth/create-staff`

---

## 👥 Vai trò trong hệ thống

| Vai trò | Mô tả | Quyền |
|---|---|---|
| **Owner** | Chủ doanh nghiệp / Quản trị tối cao | Toàn quyền: tạo staff, quản lý chi nhánh |
| **Staff** | Nhân viên cửa hàng | Quyền hạn chế, do Owner quy định |
