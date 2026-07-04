# 📦 API Test Guide — Inventory Management (Xuất Nhập Kho)

> **Base URL:** `https://localhost:7109/api`
>
> **Authentication:** Bearer JWT Token
>
> **Lưu ý:** User Owner bypass toàn bộ permission check. Staff cần được gán permission cụ thể.
>
> **Permission Mapping:**
> - `STOCK_VIEW` — GET list & detail
> - `STOCK_INWARD_CREATE` — POST nhập kho
> - `STOCK_INWARD_UPDATE` — PUT nhập kho, POST confirm nhập
> - `STOCK_INWARD_DELETE` — POST cancel nhập, DELETE legacy
> - `STOCK_OUTWARD_CREATE` — POST xuất kho
> - `STOCK_OUTWARD_UPDATE` — PUT xuất kho
> - `STOCK_OUTWARD_DELETE` — POST cancel xuất kho, DELETE legacy
> - `STOCK_OUTWARD_CONFIRM` — POST confirm xuất kho (tách riêng)

---

## 🔐 Auth — Lấy JWT Token Owner

```bash
POST /api/auth/login
Content-Type: application/json

{
  "userNameOrEmail": "owner_username",
  "password": "owner_password"
}
```

```json
// ✅ Response 200
{
  "success": true,
  "message": "Login successful",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "userId": "...",
    "role": "Owner"
  }
}
```

> **Lưu token:** Copy giá trị `token`, dùng cho header `Authorization: Bearer <token>` ở tất cả API bên dưới.

---

## ═══════════════════════════════════════
## 📥 NHẬP KHO (INWARD INVENTORY)
## ═══════════════════════════════════════

### 1. GET /api/InwardInventory — Danh sách phiếu nhập

**Permission:** `STOCK_VIEW`

```bash
curl -X GET "https://localhost:7109/api/InwardInventory?pageNumber=1&pageSize=10" \
  -H "Authorization: Bearer <owner_token>"
```

| Query Param | Kiểu | Mô tả |
|---|---|---|
| `status` | string | `PENDING` \| `COMPLETED` \| `CANCELLED` \| `ALL`. Mặc định: ẩn CANCELLED |
| `ticketType` | string | `PURCHASE` \| `CUSTOMER_RETURN` \| `BALANCE_ADJUST` |
| `fromDate` | datetime | Lọc từ ngày |
| `toDate` | datetime | Lọc đến ngày |
| `branchId` | guid | Owner xem multi-branch; Staff chỉ branch của mình |
| `pageNumber` | int | Mặc định 1 |
| `pageSize` | int | Mặc định 10, max 100 |

```json
// ✅ Response 200
{
  "success": true,
  "message": "Inward inventory history retrieved successfully",
  "data": {
    "items": [
      {
        "stockTicketId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "ticketCode": "PUR-20250702143022",
        "ticketType": "PURCHASE",
        "status": "PENDING",
        "branchId": "...",
        "userId": "...",
        "userName": "Nguyễn Văn Owner",
        "reason": "Nhập hàng từ NCC",
        "note": "Hàng về đợt 1",
        "supplierId": null,
        "createdAt": "2025-07-02T14:30:22",
        "completedAt": null,
        "confirmedBy": null,
        "cancelledAt": null,
        "cancelledBy": null,
        "cancelReason": null,
        "items": [
          {
            "ticketItemId": "...",
            "branchProductId": "...",
            "productId": "...",
            "productCode": "SP001",
            "productName": "Sữa tươi Vinamilk",
            "systemQuantity": 100.0,
            "quantity": 50.0,
            "actualQuantity": 100.0,
            "costPrice": 25000.0
          }
        ],
        "costPrice": 0
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1,
    "hasNextPage": false,
    "hasPreviousPage": false
  }
}
```

---

### 2. GET /api/InwardInventory/{id} — Chi tiết phiếu nhập

**Permission:** `STOCK_VIEW`

```bash
curl -X GET "https://localhost:7109/api/InwardInventory/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "Authorization: Bearer <owner_token>"
```

```json
// ✅ Response 200 — data là 1 object (không phải array)
{
  "success": true,
  "message": "Inward inventory retrieved successfully",
  "data": {
    "stockTicketId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "ticketCode": "PUR-20250702143022",
    "ticketType": "PURCHASE",
    "status": "PENDING",
    "branchId": "...",
    "userId": "...",
    "userName": "Nguyễn Văn Owner",
    "reason": "Nhập hàng từ NCC",
    "note": "Hàng về đợt 1",
    "createdAt": "2025-07-02T14:30:22",
    "completedAt": null,
    "cancelledAt": null,
    "confirmedBy": null,
    "cancelledBy": null,
    "cancelReason": null,
    "items": [ /* ... */ ]
  }
}

// ❌ Response 404
{
  "success": false,
  "message": "Inward inventory not found",
  "errors": ["No inward inventory with ID: ..."]
}
```

---

### 3. POST /api/InwardInventory — Tạo phiếu nhập mới

**Permission:** `STOCK_INWARD_CREATE`

> **Quy tắc:** Tạo với `status = PENDING`, KHÔNG cập nhật tồn kho tại đây. Phải gọi `/confirm` sau.

**InwardInventoryType enum:**
```json
1 = Purchase          → TicketType = "PURCHASE"
2 = CustomerReturn     → TicketType = "CUSTOMER_RETURN"
3 = BalanceAdjust      → TicketType = "BALANCE_ADJUST"
```

```bash
curl -X POST "https://localhost:7109/api/InwardInventory" \
  -H "Authorization: Bearer <owner_token>" \
  -H "Content-Type: application/json" \
  -d '{
    "inwardType": 1,
    "supplierId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "reason": "Nhập hàng từ nhà cung cấp",
    "note": "Đợt hàng tháng 7/2025",
    "items": [
      {
        "id": "b2c3d4e5-f6a7-8901-bcde-f23456789012",
        "quantity": 100,
        "costPrice": 25000,
        "note": "Lô 001"
      },
      {
        "id": "c3d4e5f6-a7b8-9012-cdef-345678901234",
        "quantity": 50,
        "costPrice": 45000,
        "note": "Lô 002"
      }
    ]
  }'
```

| Field | Bắt buộc | Mô tả |
|---|---|---|
| `inwardType` | ✅ | Enum: 1 = Purchase, 2 = CustomerReturn, 3 = BalanceAdjust |
| `reason` | ✅ | Lý do nhập kho |
| `items` | ✅ | Array, tối thiểu 1 item |
| `items[].id` | ✅ | BranchProductId hoặc ProductId |
| `items[].quantity` | ✅ | Số lượng nhập > 0 |
| `items[].costPrice` | | Giá vốn (cập nhật CostPrice cho BranchProduct) |
| `supplierId` | | GUID nhà cung cấp |
| `note` | | Ghi chú phiếu |

```json
// ✅ Response 200
{
  "success": true,
  "message": "Inward inventory created successfully. Please confirm to update stock.",
  "data": {
    "ticketId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "ticketCode": "PUR-20250702150000",
    "ticketType": "PURCHASE",
    "branchId": "...",
    "status": "PENDING"
  }
}

// ❌ Response 400 — Validation fail
{
  "success": false,
  "message": "Validation failed",
  "errors": ["Quantity must be greater than 0 for Id ..."]
}
```

---

### 4. PUT /api/InwardInventory/{id} — Cập nhật phiếu nhập

**Permission:** `STOCK_INWARD_UPDATE`

> **Quy tắc:**
> - `PENDING` → sửa Reason, Note
> - `PENDING` → chỉ cho set `Status = CANCELLED` (không cho set `COMPLETED` để bypass confirm)
> - `COMPLETED` → chỉ được sửa Note
> - `CANCELLED` → không cho sửa

```bash
curl -X PUT "https://localhost:7109/api/InwardInventory/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "Authorization: Bearer <owner_token>" \
  -H "Content-Type: application/json" \
  -d '{
    "reason": "Cập nhật lý do nhập kho",
    "note": "Ghi chú bổ sung",
    "status": "CANCELLED"
  }'
```

```json
// ✅ Response 200
{
  "success": true,
  "message": "Inward inventory updated successfully",
  "data": {
    "stockTicketId": "...",
    "ticketCode": "PUR-20250702150000",
    "ticketType": "PURCHASE",
    "status": "CANCELLED",
    "branchId": "...",
    "userId": "...",
    "userName": "Nguyễn Văn Owner",
    "reason": "Cập nhật lý do nhập kho",
    "note": "Ghi chú bổ sung",
    "createdAt": "2025-07-02T15:00:00",
    "items": [ /* ... */ ]
  }
}

// ❌ Response 400 — CANCELLED ticket
{
  "success": false,
  "message": "Cannot update cancelled ticket",
  "errors": ["Phiếu đã bị hủy. Không thể chỉnh sửa."]
}
```

---

### 5. POST /api/InwardInventory/{id}/confirm — Xác nhận phiếu nhập

**Permission:** `STOCK_INWARD_UPDATE`

> **Quy tắc:**
> - Chỉ `PENDING` mới confirm được
> - Sau confirm: `ActualStock += Quantity` (cộng kho)
> - Set `Status = COMPLETED`, ghi audit trail

```bash
curl -X POST "https://localhost:7109/api/InwardInventory/3fa85f64-5717-4562-b3fc-2c963f66afa6/confirm" \
  -H "Authorization: Bearer <owner_token>" \
  -H "Content-Type: application/json"
```

```json
// ✅ Response 200
{
  "success": true,
  "message": "Inward inventory confirmed successfully. Stock updated.",
  "data": {
    "ticketId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "ticketCode": "PUR-20250702150000",
    "status": "COMPLETED"
  }
}

// ❌ Response 400 — Already confirmed
{
  "success": false,
  "message": "Ticket already confirmed",
  "errors": ["Phiếu đã được xác nhận trước đó."]
}

// ❌ Response 400 — Cancelled
{
  "success": false,
  "message": "Cannot confirm cancelled ticket",
  "errors": ["Phiếu đã bị hủy. Không thể xác nhận."]
}

// ❌ Response 400 — No items
{
  "success": false,
  "message": "Cannot confirm ticket without items",
  "errors": ["Phiếu không có sản phẩm nào. Vui lòng thêm sản phẩm trước khi xác nhận."]
}
```

---

### 6. POST /api/InwardInventory/{id}/cancel — Hủy phiếu nhập

**Permission:** `STOCK_INWARD_DELETE`

> **Quy tắc:**
> - `PENDING` → chuyển `CANCELLED`, không ảnh hưởng kho
> - `COMPLETED` → chuyển `CANCELLED`, đồng thời `ActualStock -= Quantity` (hoàn kho)
> - `CANCELLED` → không cho hủy lại

```bash
curl -X POST "https://localhost:7109/api/InwardInventory/3fa85f64-5717-4562-b3fc-2c963f66afa6/cancel" \
  -H "Authorization: Bearer <owner_token>" \
  -H "Content-Type: application/json" \
  -d '{
    "cancelReason": "Sai số lượng, cần tạo phiếu mới"
  }'
```

```json
// ✅ Response 200 — PENDING ticket (không rollback kho)
{
  "success": true,
  "message": "Inward inventory cancelled successfully (pending ticket, no stock change)",
  "data": {
    "ticketId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "ticketCode": "PUR-20250702150000",
    "status": "CANCELLED"
  }
}

// ✅ Response 200 — COMPLETED ticket (có rollback kho)
{
  "success": true,
  "message": "Inward inventory cancelled and stock rolled back successfully",
  "data": {
    "ticketId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "ticketCode": "PUR-20250702150000",
    "status": "CANCELLED"
  }
}

// ❌ Response 400 — Already cancelled
{
  "success": false,
  "message": "Ticket already cancelled",
  "errors": ["Phiếu đã được hủy trước đó."]
}
```

---

### 7. DELETE /api/InwardInventory/{id} — Legacy Cancel

**⚠️ DEPRECATED** — Chỉ giữ tương thích FE cũ. FE mới nên dùng **POST `/cancel`**.

**Permission:** `STOCK_INWARD_DELETE`

```bash
curl -X DELETE "https://localhost:7109/api/InwardInventory/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "Authorization: Bearer <owner_token>"
```

---

## ═══════════════════════════════════════
## 📤 XUẤT KHO (OUTWARD INVENTORY)
## ═══════════════════════════════════════

### 8. GET /api/OutwardInventory — Danh sách phiếu xuất

**Permission:** `STOCK_VIEW`

```bash
curl -X GET "https://localhost:7109/api/OutwardInventory?pageNumber=1&pageSize=10" \
  -H "Authorization: Bearer <owner_token>"
```

| Query Param | Kiểu | Mô tả |
|---|---|---|
| `status` | string | `PENDING` \| `COMPLETED` \| `CANCELLED` \| `ALL`. Mặc định: ẩn CANCELLED |
| `ticketType` | string | `RETURN_SUPPLIER` \| `WRITE_OFF` \| `TRANSFER` |
| `fromDate` | datetime | Lọc từ ngày |
| `toDate` | datetime | Lọc đến ngày |
| `branchId` | guid | Owner xem multi-branch; Staff chỉ branch của mình |
| `pageNumber` | int | Mặc định 1 |
| `pageSize` | int | Mặc định 10, max 100 |

```json
// ✅ Response 200
{
  "success": true,
  "message": "Lấy lịch sử xuất kho thành công.",
  "data": {
    "items": [
      {
        "stockTicketId": "4fb96f75-6828-5673-c4fd-3d074f77bg87",
        "ticketCode": "RTS-20250702160000",
        "ticketType": "RETURN_SUPPLIER",
        "status": "PENDING",
        "branchId": "...",
        "userId": "...",
        "userName": "Trần Thị Staff",
        "reason": "Trả hàng lỗi cho NCC",
        "createdAt": "2025-07-02T16:00:00",
        "completedAt": null,
        "cancelledAt": null,
        "confirmedBy": null,
        "cancelledBy": null,
        "cancelReason": null,
        "canConfirm": true,
        "items": [
          {
            "ticketItemId": "...",
            "branchProductId": "...",
            "productId": "...",
            "productCode": "SP001",
            "productName": "Sữa tươi Vinamilk",
            "systemQuantity": 150.0,
            "quantity": 10.0,
            "actualQuantity": 150.0
          }
        ]
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1,
    "hasNextPage": false,
    "hasPreviousPage": false
  }
}
```

---

### 9. GET /api/OutwardInventory/{id} — Chi tiết phiếu xuất

**Permission:** `STOCK_VIEW`

```bash
curl -X GET "https://localhost:7109/api/OutwardInventory/4fb96f75-6828-5673-c4fd-3d074f77bg87" \
  -H "Authorization: Bearer <owner_token>"
```

> **Response:** Tương tự GET list, nhưng `data` là 1 object, có thêm `canConfirm`.

---

### 10. POST /api/OutwardInventory — Tạo phiếu xuất mới

**Permission:** `STOCK_OUTWARD_CREATE`

> **Quy tắc:** Tạo với `status = PENDING`, KHÔNG trừ kho. Phải gọi `/confirm` sau.

**OutwardInventoryType enum:**
```json
1 = ReturnToSupplier  → TicketType = "RETURN_SUPPLIER"
2 = DamagedHaoHut     → TicketType = "WRITE_OFF"
3 = InternalUse       → TicketType = "TRANSFER"
```

```bash
curl -X POST "https://localhost:7109/api/OutwardInventory" \
  -H "Authorization: Bearer <owner_token>" \
  -H "Content-Type: application/json" \
  -d '{
    "outwardType": 1,
    "reason": "Trả hàng lỗi nhà cung cấp",
    "note": "Hàng hóa hết hạn lô 001",
    "items": [
      {
        "branchProductId": "b2c3d4e5-f6a7-8901-bcde-f23456789012",
        "quantity": 10
      }
    ]
  }'
```

| Field | Bắt buộc | Mô tả |
|---|---|---|
| `outwardType` | ✅ | Enum: 1 = ReturnToSupplier, 2 = DamagedHaoHut, 3 = InternalUse |
| `reason` | ✅ | Lý do xuất kho |
| `items` | ✅ | Array, tối thiểu 1 item |
| `items[].branchProductId` | ✅ | Phải là BranchProductId đã tồn tại ở chi nhánh |
| `items[].quantity` | ✅ | Số lượng xuất > 0 |

```json
// ✅ Response 200
{
  "success": true,
  "message": "Tạo phiếu xuất kho thành công. Vui lòng xác nhận để cập nhật tồn kho.",
  "data": {
    "ticketId": "4fb96f75-6828-5673-c4fd-3d074f77bg87",
    "ticketCode": "RTS-20250702160000",
    "ticketType": "RETURN_SUPPLIER",
    "branchId": "...",
    "status": "PENDING"
  }
}

// ❌ Response 400 — Sản phẩm không thuộc chi nhánh
{
  "success": false,
  "message": "Yêu cầu không hợp lệ",
  "errors": ["Một hoặc nhiều sản phẩm không thuộc về chi nhánh đang chọn."]
}
```

---

### 11. PUT /api/OutwardInventory/{id} — Cập nhật phiếu xuất

**Permission:** `STOCK_OUTWARD_UPDATE`

> **Quy tắc:**
> - `PENDING` → sửa Reason, Note, có thể set `Status = CANCELLED`
> - **`COMPLETED` → KHÔNG cho set trực tiếp** (phải qua `/confirm`)
> - `COMPLETED` → chỉ được sửa Note
> - `CANCELLED` → không cho sửa

```bash
curl -X PUT "https://localhost:7109/api/OutwardInventory/4fb96f75-6828-5673-c4fd-3d074f77bg87" \
  -H "Authorization: Bearer <owner_token>" \
  -H "Content-Type: application/json" \
  -d '{
    "reason": "Cập nhật lý do trả hàng",
    "note": "Bổ sung ghi chú",
    "status": "CANCELLED"
  }'
```

```json
// ✅ Response 200
{
  "success": true,
  "message": "Cập nhật thông tin phiếu thành công.",
  "data": {
    "stockTicketId": "...",
    "ticketCode": "RTS-20250702160000",
    "ticketType": "RETURN_SUPPLIER",
    "status": "CANCELLED",
    "canConfirm": false,
    "items": [ /* ... */ ]
  }
}
```

---

### 12. POST /api/OutwardInventory/{id}/confirm — Xác nhận phiếu xuất

**Permission:** `STOCK_OUTWARD_CONFIRM` *(tách riêng, không dùng STOCK_OUTWARD_UPDATE)*

> **Quy tắc:**
> - Chỉ `PENDING` mới confirm được
> - Kiểm tra tồn kho thực tế (`AvailableStock >= Quantity`) — **real-time check**
> - Sau confirm: `ActualStock -= Quantity`, `AvailableStock -= Quantity` (trừ kho)
> - Set `Status = COMPLETED`, ghi audit trail
> - **Race condition protected:** dùng `FOR UPDATE` lock row

```bash
curl -X POST "https://localhost:7109/api/OutwardInventory/4fb96f75-6828-5673-c4fd-3d074f77bg87/confirm" \
  -H "Authorization: Bearer <owner_token>" \
  -H "Content-Type: application/json"
```

```json
// ✅ Response 200
{
  "success": true,
  "message": "Xác nhận phiếu xuất kho thành công. Tồn kho đã được cập nhật.",
  "data": {
    "ticketId": "4fb96f75-6828-5673-c4fd-3d074f77bg87",
    "ticketCode": "RTS-20250702160000",
    "status": "COMPLETED"
  }
}

// ❌ Response 400 — Không đủ tồn kho
{
  "success": false,
  "message": "Không đủ tồn kho để xuất",
  "errors": [
    "Không đủ tồn kho để xuất",
    "Sản phẩm không đủ: Sữa tươi Vinamilk (tồn: 5, cần xuất: 10)"
  ]
}

// ❌ Response 400 — Already confirmed
{
  "success": false,
  "message": "Ticket already confirmed",
  "errors": ["Phiếu đã được xác nhận trước đó."]
}
```

---

### 13. POST /api/OutwardInventory/{id}/cancel — Hủy phiếu xuất

**Permission:** `STOCK_OUTWARD_DELETE`

> **Quy tắc:**
> - `PENDING` → chuyển `CANCELLED`, không ảnh hưởng kho
> - `COMPLETED` → chuyển `CANCELLED`, đồng thời `ActualStock += Quantity`, `AvailableStock += Quantity` (hoàn kho)
> - `CANCELLED` → không cho hủy lại

```bash
curl -X POST "https://localhost:7109/api/OutwardInventory/4fb96f75-6828-5673-c4fd-3d074f77bg87/cancel" \
  -H "Authorization: Bearer <owner_token>" \
  -H "Content-Type: application/json" \
  -d '{
    "cancelReason": "Khách hàng không trả lại được, giữ hàng tại kho"
  }'
```

```json
// ✅ Response 200 — PENDING (không hoàn kho)
{
  "success": true,
  "message": "Hủy phiếu xuất kho thành công (phiếu chờ, chưa ảnh hưởng tồn kho).",
  "data": {
    "ticketId": "4fb96f75-6828-5673-c4fd-3d074f77bg87",
    "ticketCode": "RTS-20250702160000",
    "status": "CANCELLED"
  }
}

// ✅ Response 200 — COMPLETED (có hoàn kho)
{
  "success": true,
  "message": "Hủy phiếu xuất kho và hoàn lại tồn kho thành công.",
  "data": {
    "ticketId": "4fb96f75-6828-5673-c4fd-3d074f77bg87",
    "ticketCode": "RTS-20250702160000",
    "status": "CANCELLED"
  }
}
```

---

### 14. DELETE /api/OutwardInventory/{id} — Legacy Cancel

**⚠️ DEPRECATED** — Chỉ giữ tương thích FE cũ. FE mới nên dùng **POST `/cancel`**.

**Permission:** `STOCK_OUTWARD_DELETE`

```bash
curl -X DELETE "https://localhost:7109/api/OutwardInventory/4fb96f75-6828-5673-c4fd-3d074f77bg87" \
  -H "Authorization: Bearer <owner_token>"
```

---

## ═══════════════════════════════════════
## 📋 FLOW NGHIỆP VỤ ĐẦY ĐỦ
## ═══════════════════════════════════════

### ✅ Flow Nhập Kho (Inward)

```
POST /InwardInventory          → Tạo phiếu PENDING (chưa cộng kho)
     ↓
POST /InwardInventory/{id}/confirm  → Cộng kho, set COMPLETED
     ↓ (optional)
POST /InwardInventory/{id}/cancel   → Hoàn kho (nếu COMPLETED), set CANCELLED
```

### ✅ Flow Xuất Kho (Outward)

```
POST /OutwardInventory          → Tạo phiếu PENDING (chưa trừ kho)
     ↓
POST /OutwardInventory/{id}/confirm  → Trừ kho (real-time validation), set COMPLETED
     ↓ (optional)
POST /OutwardInventory/{id}/cancel   → Hoàn kho (nếu COMPLETED), set CANCELLED
```

### ✅ PUT /{id} — Chỉnh sửa PENDING

```
PUT /InwardInventory/{id}   → Chỉ cho sửa Reason/Note
PUT /OutwardInventory/{id}  → Chỉ cho sửa Reason/Note
                             → Chỉ cho set Status = CANCELLED (KHÔNG cho COMPLETED)
```

---

## ═══════════════════════════════════════
## 🔑 PERMISSION MATRIX
## ═══════════════════════════════════════

| Action | Owner | Staff + Permission |
|--------|-------|---------------------|
| GET List | ✅ bypass | ✅ STOCK_VIEW |
| GET Detail | ✅ bypass | ✅ STOCK_VIEW |
| POST Create | ✅ bypass | ✅ STOCK_INWARD_CREATE / STOCK_OUTWARD_CREATE |
| PUT Update | ✅ bypass | ✅ STOCK_INWARD_UPDATE / STOCK_OUTWARD_UPDATE |
| POST Confirm | ✅ bypass | ✅ STOCK_INWARD_UPDATE / **STOCK_OUTWARD_CONFIRM** |
| POST Cancel | ✅ bypass | ✅ STOCK_INWARD_DELETE / STOCK_OUTWARD_DELETE |
| DELETE (legacy) | ✅ bypass | ✅ STOCK_INWARD_DELETE / STOCK_OUTWARD_DELETE |

---

## 🧪 TEST CHECKLIST

### Nhập Kho
- [ ] Tạo phiếu PENDING → status = PENDING, kho không đổi
- [ ] Confirm PENDING → status = COMPLETED, kho tăng
- [ ] Confirm COMPLETED → lỗi 400 "already confirmed"
- [ ] Confirm CANCELLED → lỗi 400 "cannot confirm"
- [ ] Cancel PENDING → status = CANCELLED, kho không đổi, message đúng
- [ ] Cancel COMPLETED → status = CANCELLED, kho giảm (rollback), message đúng
- [ ] PUT COMPLETED → chỉ sửa được Note
- [ ] PUT PENDING status=COMPLETED → lỗi (phải qua confirm)
- [ ] PUT CANCELLED → lỗi 400 "cannot update cancelled"

### Xuất Kho
- [ ] Tạo phiếu PENDING → status = PENDING, kho không đổi
- [ ] Confirm PENDING đủ kho → status = COMPLETED, kho giảm
- [ ] Confirm PENDING thiếu kho → lỗi 400 "không đủ tồn kho"
- [ ] Confirm COMPLETED → lỗi 400
- [ ] Cancel PENDING → message "chưa ảnh hưởng tồn kho"
- [ ] Cancel COMPLETED → message "hoàn lại tồn kho", kho tăng lại
- [ ] PUT PENDING status=COMPLETED → lỗi (bypass confirm bị block)
- [ ] GET detail → canConfirm = true/false đúng theo permission
