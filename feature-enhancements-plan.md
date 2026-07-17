# Kế hoạch Triển khai Tính năng: Quản lý Vị trí, Thuộc tính & Xóa mềm Nhân sự

Plan này được tạo ra để phân chia công việc rõ ràng giữa Backend (BE) và Frontend (FE). 
- **Backend**: Tự sửa (Sử dụng lại bảng `ProductTaxonomies` cho Vị trí/Thuộc tính và cập nhật logic `OwnerStaffController` cho Xóa mềm).
- **Frontend**: Gửi plan này cho bạn phụ trách FE để tạo UI và tích hợp API.

---

## 1. Quản lý Vị trí (Location) & Quản lý Thuộc tính (Attribute)

### 📌 Backend (BE)
Thay vì tạo bảng mới, chúng ta có thể tận dụng bảng `ProductTaxonomies` hiện tại (đang dùng cho Nhóm hàng và Thương hiệu) bằng cách thêm Type mới là `"Location"` và `"Attribute"`.

1. **API Quản lý Vị trí (Locations):**
   - Thêm các endpoints trong `ProductsController.cs` (hoặc controller riêng) tương tự như Category/Brand:
     - `GET /api/products/locations` (Lấy danh sách vị trí, `Type == "Location"`)
     - `POST /api/products/locations` (Thêm mới vị trí)
     - `PUT /api/products/locations/{id}` (Sửa tên vị trí)
     - `DELETE /api/products/locations/{id}` (Xóa vị trí)

2. **API Quản lý Thuộc tính (Attributes):**
   - Thêm các endpoints tương tự:
     - `GET /api/products/attributes` (Lấy danh sách thuộc tính, `Type == "Attribute"`)
     - `POST /api/products/attributes` (Thêm mới)
     - `PUT /api/products/attributes/{id}` (Sửa)
     - `DELETE /api/products/attributes/{id}` (Xóa)

*(Lưu ý DB: Trường `ShelfLocation` trong `BranchProduct` và `AttributeName` trong `ProductAttribute` vẫn giữ nguyên kiểu `string`. FE sẽ gọi API lấy danh sách Locations/Attributes để hiển thị dropdown cho người dùng chọn thay vì nhập tay).*

### 🎨 Frontend (FE)
1. **Giao diện Quản lý danh mục:**
   - Tạo thêm 2 Popups (Modals) hoặc Màn hình quản lý: **"Quản lý vị trí"** và **"Quản lý thuộc tính"**.
   - Giao diện làm tương tự như popup "Quản lý nhóm hàng / thương hiệu" hiện có.
   - Tích hợp các API CRUD mà BE đã cung cấp ở trên.
   
2. **Cập nhật Màn hình Thêm/Sửa Sản phẩm:**
   - Ở ô nhập **Vị trí (Shelf Location)**: Đổi từ input text sang Dropdown (Select/Autocomplete). Lấy data từ API `GET /api/products/locations`. Có thể tích hợp nút (+) để mở nhanh popup thêm Vị trí.
   - Ở phần nhập **Thuộc tính (Tên thuộc tính - Size, Color...)**: Đổi từ input text sang Dropdown. Lấy data từ API `GET /api/products/attributes`. Có thể tích hợp nút (+) thêm nhanh Thuộc tính.

---

## 2. Tính năng Xóa mềm & Cảnh báo liên kết Nhân sự

### 📌 Backend (BE)
1. **API Kiểm tra liên kết dữ liệu:**
   - Thêm endpoint `GET /api/owner/staff/{id}/check-relations`.
   - Logic: Truy vấn xem `UserId` này có tồn tại trong các bảng như `Invoices` (người tạo hóa đơn), `Orders`, `StockTickets`, `Shifts`... hay không.
   - Trả về JSON: `{ hasRelations: true/false, relatedCount: 15, message: "Nhân viên đã tạo 15 hóa đơn/phiếu..." }`.

2. **Cập nhật API Delete (Xóa mềm):**
   - Sửa hàm `DeleteStaff(Guid id)` trong `OwnerStaffController.cs`.
   - **Xóa cứng -> Xóa mềm:** Không gọi `_context.Users.Remove()`. Thay vào đó:
     - Gán `staff.Status = "DELETED"` (hoặc `"INACTIVE"`).
     - Gán `staff.IsActive = 0`.
     - (Tùy chọn) Xóa các quyền (`UserPermissions`) và phân công chi nhánh (`StaffAssignments`) để tài khoản này mất hoàn toàn quyền truy cập.
   - Lưu ý các API lấy danh sách nhân viên cần cập nhật thêm điều kiện `Status != "DELETED"` để không hiển thị nhân viên đã xóa.

### 🎨 Frontend (FE)
1. **Xử lý UX khi bấm nút Xóa:**
   - Khi Admin bấm "Xóa" một nhân viên, FE khoan gọi API Delete ngay.
   - FE gọi API `GET /api/owner/staff/{id}/check-relations` để kiểm tra.
   - **Trường hợp 1 (Có liên kết):** Hiện Modal cảnh báo (như video Zalo) với nội dung: 
     > *"Tài khoản nhân viên này đang liên kết với các hóa đơn/báo cáo/đơn hàng. Nếu tiếp tục xóa, tài khoản sẽ được vô hiệu hóa để bảo toàn lịch sử dữ liệu, nhưng nhân viên sẽ không thể đăng nhập được nữa. Bạn có chắc chắn muốn tiếp tục?"*
   - **Trường hợp 2 (Không có liên kết):** Hiện Modal xác nhận xóa bình thường.

2. **Cập nhật UI Danh sách nhân viên:**
   - (Tùy thuộc yêu cầu kinh doanh) Có thể thêm 1 tab "Nhân viên đã nghỉ/xóa" để Admin có thể xem lại những tài khoản bị xóa mềm, hoặc mặc định ẩn đi khỏi danh sách chính.

---

## 🚀 Các bước thực hiện tiếp theo
- [ ] BE: Code các API CRUD cho Locations và Attributes (sử dụng ProductTaxonomies).
- [ ] BE: Code API `check-relations` cho Staff.
- [ ] BE: Sửa lại API `DeleteStaff` thành logic xóa mềm (Soft Delete).
- [ ] FE: Dựa vào Plan này để xây dựng Popups và cập nhật logic nút Xóa nhân viên.
