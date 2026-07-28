# Testing Guide cho DuyAnh

> **Mục đích**: Hướng dẫn DuyAnh test các fix + feature mới từ session validate ngày 27/07/2026.
> **Repo BE**: `namphhe181352/MEP-System` branch `namph-monolith`
> **Repo FE**: `binhlethanh201/Metal-ERP` branch `feature/signalr-notifications`

---

## 📦 Trước khi test

### 1. Pull code mới

```bash
# BE
cd "C:\Users\Home\Documents\MEP-System"
git pull origin namph-monolith

# FE
cd "C:\Users\Home\Documents\Metal-ERP"
git pull origin feature/signalr-notifications
```

### 2. Apply SHIFT permissions mới vào DB

⚠️ **Quan trọng**: Migration `SeedShiftCrudPermissions` có thể fail do FK constraint. Cách nhanh nhất là chạy SQL thẳng:

Mở pgAdmin/DBeaver/Postico, kết nối DB (`Host=103.75.182.211; Database=M.E.P Database`), chạy:

```sql
-- 1. Insert 3 permission SHIFT_CRUD mới (nếu chưa có)
INSERT INTO permissions (permission_id, permission_code, permission_name)
VALUES
    ('00000000-0000-0000-0000-000000000060', 'SHIFT_CREATE', 'Mở ca làm việc'),
    ('00000000-0000-0000-0000-000000000061', 'SHIFT_UPDATE', 'Chốt ca làm việc'),
    ('00000000-0000-0000-0000-000000000062', 'SHIFT_DELETE', 'Hủy ca làm việc')
ON CONFLICT (permission_id) DO NOTHING;

-- 2. Gán cho Owner (role 002) — VIEW + CREATE + UPDATE (không DELETE)
-- Lưu ý: Admin KHÔNG cần SHIFT vì không bán hàng. SHIFT chỉ cho Owner + SalesStaff.
INSERT INTO role_permissions (role_id, permission_id)
VALUES
    ('10000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000060'),
    ('10000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000061')
ON CONFLICT DO NOTHING;

-- 3. Gán cho SalesStaff (role 003) — VIEW + CREATE + UPDATE
INSERT INTO role_permissions (role_id, permission_id)
VALUES
    ('10000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000060'),
    ('10000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000061')
ON CONFLICT DO NOTHING;

-- 4. Gán SHIFT_VIEW cho cả Owner + SalesStaff (để họ xem được ca)
INSERT INTO role_permissions (role_id, permission_id)
VALUES
    ('10000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000034'),
    ('10000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000034')
ON CONFLICT DO NOTHING;

-- KHÔNG gán SHIFT cho Admin (001) và InventoryStaff (004) — họ không bán hàng

-- Verify
SELECT rp.role_id, p.permission_code
FROM role_permissions rp
JOIN permissions p ON p.permission_id = rp.permission_id
WHERE p.permission_code LIKE 'SHIFT_%'
ORDER BY rp.role_id, p.permission_code;
```

Kết quả phải có:
```
role_id: 10000000-...-001 (Admin) → SHIFT_CREATE, SHIFT_DELETE, SHIFT_UPDATE
role_id: 10000000-...-002 (Owner) → SHIFT_CREATE, SHIFT_UPDATE
role_id: 10000000-...-003 (Manager) → SHIFT_CREATE, SHIFT_UPDATE
```

### 3. Build & run

```powershell
# Terminal 1 — BE
cd "C:\Users\Home\Documents\MEP-System\MEP_Management_System"
dotnet build
dotnet run    # BE chạy ở https://localhost:7XXX

# Terminal 2 — FE
cd "C:\Users\Home\Documents\Metal-ERP"
npm install    # nếu chưa có node_modules
npm start      # FE chạy ở http://localhost:3000
```

---

## 🧪 Test Checklist

### Test #1: Staff view filter (Fix BROKEN)

**Mục đích**: Verify 3 tab "Hoạt động / Đã ẩn / Đã xoá" hiển thị đúng data.

| Step | Action | Expected |
|---|---|---|
| 1.1 | Login với `Owner` → `/owner/employees` | Tab "Hoạt động" hiển thị nhân viên IsActive=1 |
| 1.2 | Click tab "Đã ẩn" | Hiển thị nhân viên IsActive=0 (trước đây tab này trả về 0 rows) |
| 1.3 | Click tab "Đã xoá" | Hiển thị nhân viên Status="DELETED" |
| 1.4 | Click row bất kỳ → Modal mở | OK |
| 1.5 | Trong Modal click "Vô hiệu hoá" → confirm | **Modal đóng + alert** (trước đây Modal KHÔNG đóng → bug) |
| 1.6 | Click row → Modal → "Xoá" → confirm | **Modal đóng + alert** |
| 1.7 | Tab "Đã ẩn" → bảng có cột "Thao tác" với 2 nút | Nút xanh (Kích hoạt) + nút đỏ (Xoá vĩnh viễn) |
| 1.8 | Click nút trong cột Thao tác | **KHÔNG mở Modal** (e.stopPropagation hoạt động) |
| 1.9 | Click "Xoá vĩnh viễn" → confirm → API call | DB xóa cứng (BE `OwnerStaffController.PermanentDeleteStaff` anonymize PII) |

### Test #2: SHIFT permissions (Fix WARNING)

**Mục đích**: Verify user chỉ có SHIFT_VIEW không thể mở ca.

**Setup**: Tạo user test hoặc dùng user có sẵn. Cần 2 users:
- User A: role SalesStaff (003), chỉ có permission SHIFT_VIEW
- User B: role Owner (002), có đầy đủ SHIFT_VIEW + SHIFT_CREATE + SHIFT_UPDATE

| Step | Action | Expected |
|---|---|---|
| 2.1 | Login User A → vào POS → thử mở ca | **403 Forbidden** hoặc button "Mở ca" không hiển thị |
| 2.2 | Login User B → vào POS → mở ca | OK, mở ca thành công |
| 2.3 | Postman: POST `/api/pos/shifts/start` với token của User A | **403 Forbidden** |

### Test #3: Phiếu chi D2 cleanup (Fix)

**Mục đích**: Phiếu chi nội bộ không còn field/dropdown Nhà cung cấp.

| Step | Action | Expected |
|---|---|---|
| 3.1 | Login Owner → `/inventory/expense-management` | Bảng list **KHÔNG có cột "Nhà cung cấp"** |
| 3.2 | Click icon Filter → Drawer mở | **KHÔNG có dropdown Nhà cung cấp** |
| 3.3 | Click "Tạo phiếu chi" | Modal mở, **KHÔNG có dropdown Nhà cung cấp** |
| 3.4 | Submit form → check Network tab (F12) | Payload KHÔNG có `supplierId` |
| 3.5 | Reload page → bảng vẫn list OK | OK |

### Test #4: POS switch user (Fix)

**Mục đích**: Khi switch user, POS shift cũ phải bị xoá.

| Step | Action | Expected |
|---|---|---|
| 4.1 | Login User A → POS → mở ca | OK |
| 4.2 | F12 → Application → LocalStorage → check `pos_active_shift` | Có data (shift của A) |
| 4.3 | **KHÔNG logout**, mở tab mới → `/auth/login` → login User B | OK |
| 4.4 | F12 → LocalStorage → `pos_active_shift` | **Phải = null** (đã xoá ca của A) |

### Test #5: Owner Audit Logs page (NEW)

**Mục đích**: Page mới hiển thị nhật ký hoạt động.

| Step | Action | Expected |
|---|---|---|
| 5.1 | Login Owner → gõ URL `/audit-logs` | Page load (chưa có menu, gõ URL trực tiếp) |
| 5.2 | Filter bar | action, userId, level (ALL/INFO/WARN/ERROR), entityType, fromDate, toDate + Search + Reset |
| 5.3 | Click Search | Load list log entries |
| 5.4 | Bảng 7 cột | Thời gian, User, Action, Table, Entity, Level (Badge), Description |
| 5.5 | Phân trang | pageSize selector (20/50/100) + prev/next |

### Test #6: Owner Outward Excel page (NEW)

| Step | Action | Expected |
|---|---|---|
| 6.1 | Login Owner → gõ URL `/outward-excel` | Page load |
| 6.2 | Click "Tải template Excel" | File `Template_XuatKho_Excel.xlsx` download |
| 6.3 | Điền data vào file Excel → Upload | Preview rows + groups + errors |
| 6.4 | Click "Commit" | POST `/api/outwardinventoryexcel/commit` → tạo ticket |
| 6.5 | Check BE log | Thấy outbound tickets mới |

### Test #7: OutwardInventoryController FOR UPDATE lock (KHÔNG THAY ĐỔI BEHAVIOR)

**Mục đích**: Verify transaction lock vẫn hoạt động sau khi suppress EF1002 warning.

| Step | Action | Expected |
|---|---|---|
| 7.1 | Tạo phiếu xuất kho bình thường | OK |
| 7.2 | Concurrent: 2 request xuất kho cùng lúc | Một cái thành công, một cái fail validation (đủ stock) |

---

## ⚠️ Lưu ý

### KHÔNG push lên `main`
- BE push lên `namph-monolith`
- FE push lên `feature/signalr-notifications`

### Migration SQL nếu dùng cách migration EF Core
Nếu muốn dùng `dotnet ef database update` thay vì SQL thủ công, file migration tên `Migrations/20260727072534_SeedShiftCrudPermissions.cs` đã có trong source. Nhưng nếu fail FK constraint, dùng SQL thẳng ở trên.

### Pages chưa có menu sidebar
`/audit-logs` và `/outward-excel` đã có route nhưng **chưa wire vào sidebar**. Gõ URL trực tiếp để test. Sau khi ổn sẽ có session riêng để wire menu.

### Phối hợp với team
- **Phương Hải Nam**: đang sửa status "Còn nợ / Hết nợ" cho SupplierPayment. KHÔNG đụng file này.
- **DuyAnh**: đang sửa SupplierDebtManagement (bỏ 2 cột + đổi tên 3 cột). KHÔNG đụng file này.
- **BE `AuthController.cs` (admin register)**: bạn tôi #1 đang sửa. KHÔNG đụng.

---

## 📞 Liên hệ nếu có bug

Ghi log vào issue trong repo, tag tôi (NampH). Format:
- Repro steps
- Expected vs Actual
- Screenshot (nếu có)
- Browser + version

---

## ✅ Khi test xong tất cả PASS

Đánh dấu các mục trong checklist, comment vào PR/issue để tôi biết.
Nếu có bug, **KHÔNG tự fix**, báo lại để tôi xử lý.