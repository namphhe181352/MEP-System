# API Documentation - Module Phiếu Xuất Tiền (Expense Voucher)

## Base URL
```
http://localhost:5100/api
```

## Authentication
Tất cả API cần Bearer Token trong header:
```
Authorization: Bearer <token>
```

---

## 1. PHIẾU CHI TIỀN (Expense Voucher)

### 1.1. Lấy danh sách phiếu chi

**Endpoint:** `GET /api/expense`

**Permission:** `EXPENSE_VIEW`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| pageNumber | int | No | Trang hiện tại (default: 1) |
| pageSize | int | No | Số item/trang (default: 20, max: 100) |
| categoryId | GUID | No | Lọc theo nhóm chi phí |
| supplierId | GUID | No | Lọc theo nhà cung cấp |
| status | string | No | PENDING / COMPLETED / CANCELLED / ALL |
| fromDate | date | No | Từ ngày (YYYY-MM-DD) |
| toDate | date | No | Đến ngày (YYYY-MM-DD) |
| sort | string | No | code / amount / status / createdat / category |
| order | string | No | asc / desc |

**Response (200):**
```json
{
  "success": true,
  "message": "Lấy danh sách phiếu chi tiền thành công.",
  "data": {
    "items": [
      {
        "voucherId": "uuid",
        "voucherCode": "PXT-000001",
        "branchId": "uuid",
        "branchName": "Chi nhánh 1",
        "categoryId": "uuid",
        "categoryName": "Điện nước",
        "supplierId": "uuid|null",
        "supplierName": "Nhà cung cấp A|null",
        "amount": 1500000,
        "reason": "Thanh toán tiền điện tháng 6",
        "note": "Hóa đơn điện",
        "status": "PENDING",
        "createdBy": "uuid",
        "createdByName": "Nguyễn Văn A",
        "createdAt": "2026-07-07T10:30:00Z",
        "confirmedBy": "uuid|null",
        "confirmedByName": "Nguyễn Văn A|null",
        "completedAt": "2026-07-07T10:30:00Z|null",
        "cancelledBy": "uuid|null",
        "cancelledByName": "Nguyễn Văn A|null",
        "cancelledAt": "2026-07-07T10:30:00Z|null",
        "cancelReason": "Lý do hủy|null",
        "canConfirm": true,
        "canCancel": true
      }
    ],
    "totalCount": 100,
    "pageNumber": 1,
    "pageSize": 20,
    "totalPages": 5,
    "hasNextPage": true,
    "hasPreviousPage": false
  }
}
```

---

### 1.2. Lấy chi tiết phiếu chi

**Endpoint:** `GET /api/expense/{id}`

**Permission:** `EXPENSE_VIEW`

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| id | GUID | Yes | Voucher ID |

**Response (200):** Tương tự 1 item trong list

**Response (404):**
```json
{
  "success": false,
  "message": "Không tìm thấy phiếu chi tiền.",
  "errors": ["No expense voucher with ID: {id}"]
}
```

---

### 1.3. Tạo phiếu chi mới

**Endpoint:** `POST /api/expense`

**Permission:** `EXPENSE_CREATE`

**Request Body:**
```json
{
  "categoryId": "uuid",        // Bắt buộc - Nhóm chi phí
  "supplierId": "uuid|null",   // Nullable - Thanh toán NCC (nếu có)
  "amount": 1500000,           // Bắt buộc - Số tiền (phải > 0)
  "reason": "Thanh toán tiền điện",  // Bắt buộc - Lý do chi
  "note": "Hóa đơn điện"      // Nullable - Ghi chú thêm
}
```

**Validation:**
- `amount` phải > 0
- `reason` không được rỗng
- `categoryId` phải tồn tại và IsActive = 1
- `supplierId` (nếu có) phải tồn tại và Status = 1

**Response (200):**
```json
{
  "success": true,
  "message": "Tạo phiếu chi tiền thành công.",
  "data": {
    "voucherId": "uuid",
    "voucherCode": "PXT-000001",
    "branchId": "uuid",
    "categoryId": "uuid",
    "categoryName": "Điện nước",
    "supplierId": null,
    "amount": 1500000,
    "reason": "Thanh toán tiền điện",
    "note": "Hóa đơn điện",
    "status": "PENDING",
    "createdBy": "uuid",
    "createdByName": "Nguyễn Văn A",
    "createdAt": "2026-07-07T10:30:00Z",
    "canConfirm": true,
    "canCancel": true
  }
}
```

---

### 1.4. Xác nhận phiếu chi

**Endpoint:** `PATCH /api/expense/{id}/confirm`

**Permission:** `EXPENSE_CONFIRM`

**Business Rules:**
- Chỉ phiếu có `status = PENDING` mới được xác nhận
- Owner luôn được xác nhận
- Hoặc user có permission `EXPENSE_CONFIRM`

**Response (200):**
```json
{
  "success": true,
  "message": "Xác nhận phiếu chi tiền thành công.",
  "data": {
    "voucherId": "uuid",
    "voucherCode": "PXT-000001",
    "status": "COMPLETED",
    "completedAt": "2026-07-07T11:00:00Z",
    "confirmedBy": "uuid",
    "confirmedByName": "Nguyễn Văn A"
  }
}
```

**Response (400) - Đã xác nhận:**
```json
{
  "success": false,
  "message": "Phiếu đã được xác nhận trước đó."
}
```

**Response (400) - Đã hủy:**
```json
{
  "success": false,
  "message": "Phiếu đã bị hủy. Không thể xác nhận."
}
```

---

### 1.5. Hủy phiếu chi

**Endpoint:** `PATCH /api/expense/{id}/cancel`

**Permission:** `EXPENSE_CANCEL`

**Request Body:**
```json
{
  "cancelReason": "Lý do hủy phiếu"  // Bắt buộc
}
```

**Business Rules:**
- Chỉ phiếu có `status = PENDING` mới được hủy
- Owner luôn được hủy
- Hoặc user có permission `EXPENSE_CANCEL`

**Response (200):**
```json
{
  "success": true,
  "message": "Hủy phiếu chi tiền thành công.",
  "data": {
    "voucherId": "uuid",
    "voucherCode": "PXT-000001",
    "status": "CANCELLED",
    "cancelledAt": "2026-07-07T11:00:00Z",
    "cancelledBy": "uuid",
    "cancelledByName": "Nguyễn Văn A",
    "cancelReason": "Lý do hủy phiếu"
  }
}
```

---

## 2. NHÓM CHI PHÍ (Expense Category)

### 2.1. Lấy danh sách nhóm chi phí

**Endpoint:** `GET /api/expense/categories`

**Permission:** `EXPENSE_VIEW`

**Response (200):**
```json
{
  "success": true,
  "message": "Lấy danh sách nhóm chi phí thành công.",
  "data": [
    {
      "categoryId": "uuid",
      "categoryName": "Điện nước",
      "isActive": 1,
      "voucherCount": 15,
      "pendingCount": 3
    },
    {
      "categoryId": "uuid",
      "categoryName": "Lương & Phụ cấp",
      "isActive": 1,
      "voucherCount": 8,
      "pendingCount": 1
    }
  ]
}
```

---

### 2.2. Tạo nhóm chi phí mới

**Endpoint:** `POST /api/expense/categories`

**Permission:** `EXPENSE_CATEGORY_MANAGE`

**Request Body:**
```json
{
  "categoryName": "Chi phí vận chuyển"  // Bắt buộc, unique
}
```

**Validation:**
- `categoryName` không được rỗng
- `categoryName` không được trùng với nhóm đã có

**Response (200):**
```json
{
  "success": true,
  "message": "Tạo nhóm chi phí thành công.",
  "data": {
    "categoryId": "uuid",
    "categoryName": "Chi phí vận chuyển",
    "isActive": 1,
    "voucherCount": 0,
    "pendingCount": 0
  }
}
```

---

### 2.3. Sửa tên nhóm chi phí

**Endpoint:** `PUT /api/expense/categories/{id}`

**Permission:** `EXPENSE_CATEGORY_MANAGE`

**Request Body:**
```json
{
  "categoryName": "Chi phí logistics"  // Bắt buộc
}
```

**Response (200):**
```json
{
  "success": true,
  "message": "Đổi tên nhóm chi phí 'Chi phí vận chuyển' thành 'Chi phí logistics' thành công.",
  "data": {
    "categoryId": "uuid",
    "categoryName": "Chi phí logistics",
    "isActive": 1,
    "voucherCount": 0,
    "pendingCount": 0
  }
}
```

---

### 2.4. Xóa nhóm chi phí

**Endpoint:** `DELETE /api/expense/categories/{id}`

**Permission:** `EXPENSE_CATEGORY_MANAGE`

**Business Rules:**
- Soft delete (set `IsActive = 0`)
- Chỉ xóa được khi không còn phiếu `PENDING` nào đang dùng nhóm này

**Response (200):**
```json
{
  "success": true,
  "message": "Xóa nhóm chi phí 'Chi phí logistics' thành công. (Có 5 phiếu chi tiền được giữ lại)"
}
```

**Response (400) - Còn phiếu PENDING:**
```json
{
  "success": false,
  "message": "Không thể xóa nhóm chi phí đang có phiếu PENDING.",
  "errors": [
    "Có 3 phiếu chi tiền đang ở trạng thái PENDING sử dụng nhóm này.",
    "Vui lòng hủy hoặc xác nhận các phiếu PENDING trước khi xóa nhóm chi phí."
  ]
}
```

---

## 3. ENUM & STATUS

### 3.1. Voucher Status
| Status | Mô tả |
|--------|-------|
| PENDING | Chờ xác nhận |
| COMPLETED | Đã xác nhận |
| CANCELLED | Đã hủy |

### 3.2. CanConfirm / CanCancel Logic
| Điều kiện | CanConfirm | CanCancel |
|------------|-----------|----------|
| Status != PENDING | false | false |
| IsOwner | true | true |
| Có permission EXPENSE_CONFIRM | true | - |
| Có permission EXPENSE_CANCEL | - | true |

---

## 4. NHÓM CHI PHÍ MẶC ĐỊNH

Khi chạy migration, hệ thống sẽ tự tạo 8 nhóm chi phí mặc định:

| ID | Tên nhóm |
|----|-----------|
| 11111111-1111-1111-1111-111111111111 | Điện nước |
| 22222222-2222-2222-2222-222222222222 | Lương & Phụ cấp |
| 33333333-3333-3333-3333-333333333333 | Thuê mặt bằng |
| 44444444-4444-4444-4444-444444444444 | Vận chuyển |
| 55555555-5555-5555-5555-555555555555 | Văn phòng phẩm |
| 66666666-6666-6666-6666-666666666666 | Bảo trì & Sửa chữa |
| 77777777-7777-7777-7777-777777777777 | Marketing & Quảng cáo |
| 88888888-8888-8888-8888-888888888888 | Chi phí khác |

---

## 5. ERROR CODES

| HTTP Status | Message | Giải thích |
|-------------|---------|-------------|
| 400 | Validation failed | Dữ liệu không hợp lệ |
| 401 | Unauthorized | Token không hợp lệ |
| 403 | Forbidden | Không có quyền |
| 404 | Not Found | Không tìm thấy resource |
| 500 | Internal Server Error | Lỗi server |

---

## 6. VÍ DỤ SỬ DỤNG

### 6.1. Tạo phiếu chi tiền điện
```bash
curl -X POST http://localhost:5100/api/expense \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "categoryId": "11111111-1111-1111-1111-111111111111",
    "amount": 2500000,
    "reason": "Thanh toán tiền điện tháng 6/2026",
    "note": "Hóa đơn số 123456"
  }'
```

### 6.2. Tạo phiếu thanh toán cho NCC
```bash
curl -X POST http://localhost:5100/api/expense \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "categoryId": "44444444-4444-4444-4444-444444444444",
    "supplierId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "amount": 15000000,
    "reason": "Thanh toán công nợ tháng 5/2026"
  }'
```

### 6.3. Lấy danh sách phiếu PENDING trong tháng
```bash
curl -X GET "http://localhost:5100/api/expense?status=PENDING&fromDate=2026-07-01&toDate=2026-07-31" \
  -H "Authorization: Bearer <token>"
```

### 6.4. Xác nhận phiếu chi
```bash
curl -X PATCH http://localhost:5100/api/expense/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/confirm \
  -H "Authorization: Bearer <token>"
```

### 6.5. Hủy phiếu chi
```bash
curl -X PATCH http://localhost:5100/api/expense/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/cancel \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "cancelReason": "Sai thông tin, cần tạo lại"
  }'
```
