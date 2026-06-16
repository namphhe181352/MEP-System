# Plan: MEP.Sale.Api - Sales Staff Backend Implementation (v3 - Updated)

## Context

Analysis derived from **6 sources**:
1. **SRS docx** (sections 2.2-2.3-1.4.1.2, UCs 32-51)
2. **OpenAPI YAML** (`api_sales_staff_openapi.yaml`, 30 endpoints - **official spec** based on SRS)
3. **POS-API.md** (markdown file from the FE team - **actual mock**, 18 endpoints)
4. **project-guide.md** (FE architecture description)
5. **FE React** (`/Metal-ERP/src/modules/pos/`, 5 pages)
6. **Current DB** (`MEP.Data/Models/`, 25 entities)

**Final decisions (per user request)**:
- **Follow SRS SPEC + YAML** (not the FE mock): keep **4 payment methods** = `Cash`, `Transfer`, `Combined`, `Debt` (PascalCase, per SRS)
- **Endpoint uses `/pos/invoices`** (YAML spec) - DO NOT change to `/pos/orders` like the FE mock
- **Debt payment (UC40) is in Phase 1** (do not skip)
- **Transfer = manual confirmation**: customer transfers → staff checks bank account → confirms (per user request)
- **Default VAT 8%** (per POS-API.md - kept from plan v2)
- **OrderCode format**: `POS-YYYYMMDD-XXX` (per POS-API.md - kept from plan v2)
- **Response**: returns object directly (per YAML - kept from old plan)
- **MEP.Sale.Api = 25 endpoints** strictly following YAML (including separate payment endpoints, QR for transfer)

**Endpoint count: 25** (full YAML, without omitting debt/QR/price/stock).

---

## Scope of Implementation

### Phase 1: Data Layer (MEP.Data)
- Create 8 new classes:
  - `Payment` - tracks each individual payment (1 invoice can have multiple Payment rows)
  - `Promotion` - validates promotion codes (keeps name `Promotion` per SRS/YAML)
  - `PromotionUsage` - tracks promo usage history
  - `Shift` - manages sales shifts, has `ShiftCode` field
  - `LoyaltyTransaction` - tracks points addition/deduction (UC51 BR-83)
  - `RefundRecord` - tracks refunds for returns (UC49)
  - `Receipt` - stores HTML + URL for each print
  - `DailySummary` - aggregates todayCount, todayRevenue, totalRevenue
- Update `Customer.cs`:
  - `+Group` (Individual/Enterprise/Agent/Contractor)
  - `+Notes` (text)
  - `+TotalSpent` (decimal - derived but cached)
  - `+OrderCount` (int - derived)
  - `+LastVisit` (DateTime - derived)
  - `+MembershipTier` (string - derived)
  - `+UpdatedAt` (DateTime) - **NEW compared to old plan**
- Update `MEPDatabaseContext.cs`: +8 DbSets + fluent API + relationships
- Update `DatabaseSeeder.cs`: seed 3 promotions, 2 shifts (1 open + 1 closed), 5 loyalty transactions
- Create 1 new migration: `AddSalesEntities`
- Update `.sln` file (already exists)

### Phase 2: MEP.Shared Extension
- Add helper class `CodeGenerator` (generates InvoiceCode, OrderCode, ShiftCode, PromotionCode, ReceiptCode)
- Copy `JwtHelper` (already exists) - no changes

### Phase 3: MEP.Sale.Api Foundation
- Update `Program.cs`: add DbContext, JWT, Swagger, CORS, AutoMapper, OData, JSON IgnoreCycles
- Update `appsettings.json`: connection string, JWT config, API port (7002)
- Update `MEP.Sale.Api.csproj`: add packages (EF Core, AutoMapper, BCrypt)

### Phase 4: DTOs + AutoMapper
- 25 DTO files in `MEP.Sale.Api/DTOs/`
- 1 Enum file `PaymentMethod.cs` (4 values: Cash/Transfer/Combined/Debt)

### Phase 5: 8 Controllers (25 endpoints - full YAML)

| # | Controller | Endpoints | Corresponding UC |
|---|---|---|---|
| 1 | `InvoicesController` (per YAML) | 8 (list, get, create, cancel, finalize, items, scan, search) | UC32-35, UC45 |
| 2 | `PaymentsController` | 4 (create, QR, confirm-transfer, debt) | UC36-40 |
| 3 | `PromotionsController` | 2 (apply, remove) | UC43 |
| 4 | `PrintingController` | 2 (print, print.pdf) | UC44 |
| 5 | `ProductsController` (proxies to Inventory) | 2 (price, stock lookup) | UC41-42 |
| 6 | `ShiftsController` | 4 (start, end, summary, list) | (FE has it, BE doesn't yet) |
| 7 | `CustomersController` (per `/pos/`) | 4 (list, create, update, orders) | UC51 |
| 8 | `LoyaltyController` | 1 (get points read-only) | UC51 BR-83 |

### Phase 6: Integration Testing
- `dotnet build` entire solution
- Apply migration to PostgreSQL
- Run Auth API + Sale API
- Test end-to-end flow

---

## Files to Create/Update

### CREATE (MEP.Sale.Api)
```
MEP.Sale.Api/
├── DTOs/
│   ├── PaymentMethod.cs                       (ENUM: Cash, Transfer, Combined, Debt)
│   ├── CreateInvoiceRequest.cs
│   ├── UpdateInvoiceItemRequest.cs
│   ├── AddItemRequest.cs
│   ├── ScanItemRequest.cs
│   ├── CashPaymentRequest.cs
│   ├── TransferPaymentRequest.cs
│   ├── CombinedPaymentRequest.cs
│   ├── DebtPaymentRequest.cs
│   ├── PaymentDto.cs
│   ├── ApplyPromoRequest.cs
│   ├── PromotionDto.cs
│   ├── CreateShiftRequest.cs
│   ├── EndShiftRequest.cs
│   ├── ShiftDto.cs
│   ├── ShiftSummaryDto.cs
│   ├── CreateCustomerRequest.cs
│   ├── UpdateCustomerRequest.cs
│   ├── CustomerDto.cs
│   ├── CustomerOrderDto.cs
│   ├── LoyaltyPointsDto.cs
│   ├── ProductPriceDto.cs
│   ├── ProductStockDto.cs
│   ├── ReceiptDto.cs
│   ├── InvoiceDto.cs
│   ├── InvoiceItemDto.cs
│   ├── PageResultDto.cs
│   └── ApiErrorResponse.cs
├── Controllers/
│   ├── InvoicesController.cs                  (8 endpoints)
│   ├── PaymentsController.cs                  (4 endpoints)
│   ├── PromotionsController.cs                (2 endpoints)
│   ├── PrintingController.cs                  (2 endpoints)
│   ├── ProductsController.cs                  (2 endpoints)
│   ├── ShiftsController.cs                    (4 endpoints)
│   ├── CustomersController.cs                 (4 endpoints)
│   └── LoyaltyController.cs                  (1 endpoint)
├── Services/
│   ├── CodeGeneratorService.cs                (generates INV/POS/SHIFT/PROMO codes)
│   └── OrderService.cs                        (logic for invoice + payment + stock)
├── AutoMapper/
│   └── SaleMappingProfile.cs
└── MEP.Sale.Api.csproj (UPDATE - add packages)
```

### UPDATE (MEP.Data)
```
MEP.Data/
├── Models/
│   ├── Customer.cs (UPDATE +Group, Notes, TotalSpent, OrderCount, LastVisit, MembershipTier, UpdatedAt)
│   ├── Payment.cs (NEW)
│   ├── Promotion.cs (NEW)
│   ├── PromotionUsage.cs (NEW)
│   ├── Shift.cs (NEW - with ShiftCode field)
│   ├── LoyaltyTransaction.cs (NEW)
│   ├── RefundRecord.cs (NEW)
│   ├── Receipt.cs (NEW)
│   ├── DailySummary.cs (NEW)
│   └── MEPDatabaseContext.cs (UPDATE +8 DbSets, fluent API)
├── Seed/
│   └── DatabaseSeeder.cs (UPDATE +promotions, +shifts, +loyalty)
└── Migrations/
    └── YYYYMMDD_AddSalesEntities.cs (NEW)
```

### UPDATE (MEP.Shared)
```
MEP.Shared/
└── Services/
    └── CodeGenerator.cs (NEW - generates INV-2026-XXXX, POS-YYYYMMDD-XXX, SHIFT-YYYYMMDD-XXX, PROMO-XXXX, RCP-XXXX, RFD-XXXX)
```

### UPDATE (MEP.Sale.Api foundation)
```
MEP.Sale.Api/
├── Program.cs (UPDATE - add services)
├── appsettings.json (UPDATE)
├── appsettings.Development.json (UPDATE)
└── Properties/launchSettings.json (UPDATE port)
```

---

## Endpoints to Implement (25 endpoints per YAML)

### Invoices (UC32-35, UC45) - 8 endpoints
| # | Method | Path | Description |
|---|---|---|---|
| 1 | GET | `/api/pos/invoices` | List invoices with filter (status, dateFrom, dateTo, customerId, staffId, page) |
| 2 | POST | `/api/pos/invoices` | Create new invoice (Draft status) |
| 3 | GET | `/api/pos/invoices/{invoiceId}` | Invoice detail |
| 4 | PATCH | `/api/pos/invoices/{invoiceId}/cancel` | Cancel invoice (only if status=Draft) |
| 5 | POST | `/api/pos/invoices/{invoiceId}/finalize` | Finalize invoice (Draft → Completed, deduct stock) |
| 6 | POST | `/api/pos/invoices/{invoiceId}/items` | Add product to invoice |
| 7 | GET | `/api/pos/invoices/{invoiceId}/items` | List items of an invoice |
| 8 | POST | `/api/pos/invoices/{invoiceId}/items/scan` | Scan barcode/QR add item |
| 9 | POST | `/api/pos/invoices/{invoiceId}/items/search` | Search product to add |

### Payments (UC36-40) - 4 endpoints
| # | Method | Path | Description |
|---|---|---|---|
| 10 | POST | `/api/pos/invoices/{invoiceId}/payments` | Create payment record (Cash/Transfer/Combined/Debt) |
| 11 | GET | `/api/pos/payments/{paymentId}/qr` | Generate QR for Transfer (VietQR string, 5min expire) |
| 12 | POST | `/api/pos/payments/{paymentId}/confirm-transfer` | Staff confirms receipt of transfer (manual check TK) |
| 13 | POST | `/api/pos/payments/debt` | Record debt for regular customers/contractors (validate debtLimit) |

### Promotions (UC43) - 2 endpoints
| # | Method | Path | Description |
|---|---|---|---|
| 14 | POST | `/api/pos/invoices/{invoiceId}/promo` | Apply promo code to invoice |
| 15 | DELETE | `/api/pos/invoices/{invoiceId}/promo` | Remove promo code from invoice |

### Printing (UC44) - 2 endpoints
| # | Method | Path | Description |
|---|---|---|---|
| 16 | POST | `/api/pos/invoices/{invoiceId}/print` | Trigger physical/PDF invoice print |
| 17 | GET | `/api/pos/invoices/{invoiceId}/print.pdf` | Download PDF invoice |

### Products lookup (UC41-42) - 2 endpoints
| # | Method | Path | Description |
|---|---|---|---|
| 18 | GET | `/api/pos/products/{productId}/price` | Check retail price + floor price |
| 19 | GET | `/api/pos/products/{productId}/stock` | Check real-time stock (cross-branch) |

### Shifts - 4 endpoints
| # | Method | Path | Description |
|---|---|---|---|
| 20 | POST | `/api/pos/shifts/start` | Start shift |
| 21 | POST | `/api/pos/shifts/end` | End shift |
| 22 | GET | `/api/pos/shifts/{shiftId}/summary` | Shift summary |
| 23 | GET | `/api/pos/shifts?from=&to=` | Shift history |

### Customers (UC51) - 4 endpoints
| # | Method | Path | Description |
|---|---|---|---|
| 24 | GET | `/api/pos/customers?search=&group=&page=&limit=` | List customers |
| 25 | POST | `/api/pos/customers` | Create new customer |
| 26 | PUT | `/api/pos/customers/{id}` | Update customer |
| 27 | GET | `/api/pos/customers/{id}/orders?page=&limit=` | Purchase history |

### Loyalty (UC51 BR-83) - 1 endpoint
| # | Method | Path | Description |
|---|---|---|---|
| 28 | GET | `/api/pos/customers/{id}/points` | View loyalty points (READ-ONLY) |

**Total: 28 endpoints** (Controller `CustomersController` has 4 endpoints, I miscounted earlier - it is exactly 28).

---

## Pattern Grounding (from current codebase)

| Category | Source | Pattern |
|---|---|---|
| Project structure | `MEP.Inventory.Api/Program.cs` | ASP.NET Core 8 + JWT + Swagger + Npgsql + AutoMapper + JSON IgnoreCycles |
| JWT config | `MEP.Inventory.Api/Program.cs:16-34` | `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Key` from appsettings |
| Authorization | `ProductsController.cs:14` | `[Authorize]` + `[Authorize(Roles = "SalesStaff")]` |
| Get user branchId from JWT | `ProductsController.cs:29-37` | `User.FindFirst("branchId")?.Value` |
| AutoMapper | `MEP.Auth.Api/Program.cs:53` | `AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies())` |
| OData filter | `MEP.Auth.Api/Program.cs:29` | `AddOData(options => options.Select().Filter().OrderBy().Count().SetMaxTop(100))` |
| JSON cycle | `MEP.Inventory.Api/Program.cs:37-41` | `ReferenceHandler.IgnoreCycles` |
| Controller naming | ProductsController, AuthController | `[ApiController] [Route("api/[controller]")]` |
| Database access | `ProductsController.cs:49-53` | `_context.BranchProducts.Include(...).Where(...).AsQueryable()` |
| Pagination | `ProductDto.cs:47-54` | `PageNumber`, `PageSize`, `SearchTerm` parameter object |
| Connection string | `MEPDatabaseContext.cs:76-83` | `"Host=103.75.182.211;Port=5432;Database=M.E.P Database;Username=postgres;Password=Vuatrom154321@"` |
| Insert seed code-first | `DatabaseSeeder.cs` | `BCrypt.HashPassword` + `context.SaveChangesAsync()` |
| Error response format | `YAML ErrorResponse` | `{error: {code: "MSG-XXX", message: "..."}}` |

---

## Business Rules to adhere to (from SRS)

| BR | Description | Implementation |
|---|---|---|
| **BR-73** | Validate invoice has ≥1 line item before payment (MSG-61) | In `FinalizeInvoice` handler |
| **BR-74** | Validate stock real-time (MSG-59) | Check `BranchProduct.AvailableStock >= item.Quantity` |
| **BR-75** | Validate price override ≥ floor price (MSG-63) | Check `unitPrice >= BranchProduct.FloorPrice` (needs FloorPrice addition) |
| **BR-76** | Validate payment = 100% total | `sum(payments.amount) >= invoice.totalAmount` |
| **BR-77** | Filter data by branchId in token | All queries: filter `BranchId == userBranchId` |
| **BR-78** | Invoice COMPLETED is immutable | Cancel is allowed only when `status == "Draft"` |
| **BR-79** | Cross-branch stock visibility | UC42 returns stock across all branches |
| **BR-80** | Return window 30 days | Filter eligible invoice (MSG-65) |
| **BR-82** | Unique phone number when creating customer | Validate before insert (MSG-68) |
| **BR-83** | Loyalty points READ-ONLY for Sales Staff | GET only, no POST/PUT |
| **MSG-84** | Debt exceeds limit | Validate `currentDebt + amount <= debtLimit` |
| **MSG-85** | Empty phone | Validate when creating customer |
| **MSG-67** | Incorrect phone format | Validate regex `^0[3|5|7|8|9][0-9]{8}$` |

---

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Old schema conflict when adding 8 tables + 7 Customer fields | Medium | High | Drop schema `public` and re-run migration `FullMEPInitialSchema` + `AddSalesEntities` |
| FE uses mock 18 endpoints, BE has 28 endpoints → mismatch | High | Medium | BE follows YAML/SRS. FE will adapt. Both might need updates |
| VAT 8% vs 10% in DB SystemConfig | Medium | Medium | Use SystemConfig.VAT_RATE from DB, default 8% (already set in seed) |
| OrderCode format `POS-YYYYMMDD-XXX` needs to be unique per day | Low | Medium | Use `CodeGenerator` with count orders of today + 1 |
| 1 invoice has multi-payment (cash + transfer = Combined) | High | Medium | Separate `Payment` table, 1 Invoice → multiple Payment rows, `Method = Combined` for multi |
| Debt payment exceeds customer limit (MSG-84) | Medium | Medium | Validate `currentDebt + amount <= debtLimit` before creating |
| Transfer manual confirm (no auto bank check) | Medium | Medium | Staff clicks "Confirm" → BE sets `Payment.Status = Confirmed` |
| QR for transfer (5min expire) | Low | Low | Generate VietQR string, store expiresAt |
| 2 APIs running simultaneously (Auth 7001 + Sale 7002) | Low | Low | Set ports in `launchSettings.json` |
| CORS blocking FE port 3000 calls | High | Medium | `AddCors` with policy `AllowReactDev` |
| Shift conflict - 1 user opens 2 shifts | Low | Medium | Validate: `UserId` does not have an OPEN Shift |
| Products from Inventory API - proxy or direct DB query | Medium | Medium | Query directly from DB (simpler), filter by branchId |
| Customer membershipTier derived (Bronze/Silver/Gold/Platinum) | Low | Low | Calculate based on TotalSpent when querying, do not store in DB |
| Return Order API (UC46-50) not in this scope | Medium | Low | Leave for Phase 2 (will do when FE has Return screen) |

---

## Estimated Complexity: **LARGE** (28 endpoints, 8 new tables, 8 controllers)

- Data layer: 2-3 hours
- DTOs + AutoMapper: 1-2 hours
- 8 Controllers × ~40-60 lines: 5-7 hours
- OrderService + PaymentService (logic for invoice + payment + stock + debt): 3-4 hours
- Business rules + validation: 2-3 hours
- Seed data + testing: 1-2 hours
- Debug + fixing: 1-2 hours
- **Total: 15-23 hours** (~3-4 work days)

---

## Acceptance Criteria

### Backend API
- [ ] `dotnet build` entire solution passes with no errors
- [ ] `MEP.Sale.Api` runs on port 7002, swagger shows all 28 endpoints
- [ ] Login with `sale.bac01@mep.vn` / `MEP@2026` → valid token
- [ ] `POST /api/pos/invoices` → returns 201 + Invoice (status=Draft)
- [ ] `POST /api/pos/invoices/{id}/items` → add 1 product, validate stock (BR-74)
- [ ] `POST /api/pos/invoices/{id}/items/scan` → scan barcode, auto add
- [ ] `POST /api/pos/invoices/{id}/items/search` → search & add
- [ ] `GET /api/pos/invoices/{id}/items` → list items
- [ ] `GET /api/pos/invoices?status=Completed&dateFrom=2026-01-01` → list with filter
- [ ] `GET /api/pos/invoices/{id}` → full detail
- [ ] `POST /api/pos/invoices/{id}/payments` with `{method: "Cash", cashReceived: 500000}` → 201, changeAmount correct
- [ ] `POST /api/pos/invoices/{id}/payments` with `{method: "Transfer"}` → 201, status=Pending
- [ ] `GET /api/pos/payments/{id}/qr` → returns VietQR string, expiresAt
- [ ] `POST /api/pos/payments/{id}/confirm-transfer` → 200, status=Confirmed
- [ ] `POST /api/pos/payments/debt` with `{customerId, amount}` → 201, customer.currentDebt increases
- [ ] `POST /api/pos/payments/debt` exceeding debtLimit → 422 + MSG-84
- [ ] `POST /api/pos/invoices/{id}/promo` with `{promoCode: "SUMMER2026"}` → 200, discountAmount applied
- [ ] `DELETE /api/pos/invoices/{id}/promo` → 204
- [ ] `POST /api/pos/invoices/{id}/finalize` → status=Completed, stock deducted (BR-74)
- [ ] `PATCH /api/pos/invoices/{id}/cancel` (only if status=Draft, BR-78) → 200
- [ ] `POST /api/pos/invoices/{id}/print` → 200, printedAt
- [ ] `GET /api/pos/invoices/{id}/print.pdf` → PDF file
- [ ] `GET /api/pos/products/{id}/price` → returns retailPrice, floorPrice
- [ ] `GET /api/pos/products/{id}/stock` → returns inStockQty, inTransitQty (cross-branch)
- [ ] `POST /api/pos/shifts/start` → creates OPEN Shift
- [ ] `POST /api/pos/shifts/{id}/end` → closes shift, calculates variance
- [ ] `GET /api/pos/shifts/{id}/summary` → returns shift summary
- [ ] `GET /api/pos/shifts?from=&to=` → list shifts
- [ ] `GET /api/pos/customers?phone=...` → search customer (BR-82)
- [ ] `POST /api/pos/customers` with `{name, phone}` → 201, validate unique phone
- [ ] `PUT /api/pos/customers/{id}` → 200, updatedAt updated
- [ ] `GET /api/pos/customers/{id}/orders` → list COMPLETED invoices
- [ ] `GET /api/pos/customers/{id}/points` → returns currentPoints, membershipTier (read-only, BR-83)
- [ ] CORS allows FE port 3000
- [ ] Error response format `{error: {code: "MSG-XXX", message: "..."}}` per YAML

### Business Rules to adhere to
- [ ] **BR-73**: Invoice must have ≥1 item before finalizing
- [ ] **BR-74**: Validate stock before adding item/finalizing
- [ ] **BR-75**: Validate price override ≥ floor price
- [ ] **BR-76**: `sum(payments.amount) >= invoice.totalAmount`
- [ ] **BR-77**: Filter by branchId from JWT
- [ ] **BR-78**: Cancel only for `status=Draft`
- [ ] **BR-79**: Cross-branch stock visibility
- [ ] **BR-82**: Unique phone when creating customer (MSG-68)
- [ ] **BR-83**: Loyalty READ-ONLY
- [ ] **MSG-84**: Debt exceeds limit
- [ ] **MSG-85**: Empty phone
- [ ] **MSG-67**: Incorrect phone format (VN regex)

---

## Out of Scope (To be done in separate Phase)

- **Return Order API** (`/returns/*` - UC46-50) - wait until FE has Return Order screen
- **Promotions Management API** (CRUD promotion from admin side) - to be done in Admin API
- **Real QR generator library** (QRCoder NuGet) - use VietQR string (text), no PNG needed
- **Email Service integration** for debt notification
- **S2S to Inventory API** to deduct stock - query directly from DB instead of S2S
- **Prometheus/metrics, detailed audit logging**

---

## Important Note on Payment Methods (4 types per spec)

Per user request, **KEEP 4 payment methods** per SRS/YAML (not the FE mock):

| Method | Enum value | Description | Status workflow |
|---|---|---|---|
| **Cash** | `Cash` | Cash - customer gives money, might have change → changeAmount | Auto Confirmed |
| **Transfer** | `Transfer` | Bank Transfer - customer transfers via QR/Account → staff checks bank → confirms | Pending → Confirmed (manual) |
| **Combined** | `Combined` | Combined payment with multiple methods in 1 invoice (cash + transfer) | Pending → Confirmed |
| **Debt** | `Debt` | Credit for regular customers/contractors - increases `customer.currentDebt`, validates debtLimit | Auto Confirmed |

**Multi-payment**: 1 invoice can have multiple Payment rows with different methods (cash + transfer), total amount = invoice totalAmount. Aggregate → method of Invoice = "Combined".

---

## Work Plan by Sprint (3-4 days)

### Sprint 1: Data + Foundation (1 day)
- [ ] Create 8 new models (Payment, Promotion, PromotionUsage, Shift, LoyaltyTransaction, RefundRecord, Receipt, DailySummary)
- [ ] Update Customer.cs with 7 new fields
- [ ] Update MEPDatabaseContext + fluent API
- [ ] Update DatabaseSeeder (promotions, shifts, loyalty)
- [ ] Create migration `AddSalesEntities`
- [ ] Setup MEP.Sale.Api: Program.cs, appsettings, port
- [ ] Create CodeGenerator + ApiErrorResponse
- [ ] **Build pass**

### Sprint 2: Invoices + Payments + Promotions (1.5 days)
- [ ] DTOs + AutoMapper (28 files)
- [ ] InvoicesController (8 endpoints) + finalize + cancel logic
- [ ] PaymentsController (4 endpoints) + Cash/Transfer/Combined/Debt validation
- [ ] PromotionsController (2 endpoints) - apply/remove
- [ ] **Build pass**

### Sprint 3: Printing + Products lookup + Shifts + CRM (1 day)
- [ ] PrintingController (2 endpoints)
- [ ] ProductsController (2 endpoints - price/stock lookup)
- [ ] ShiftsController (4 endpoints) + variance calc
- [ ] CustomersController (4 endpoints) + unique phone validation
- [ ] LoyaltyController (1 endpoint - read-only)
- [ ] **Build pass**

### Sprint 4: Testing (0.5 days)
- [ ] Additional seed data
- [ ] **End-to-end testing** with Auth + Sale API
- [ ] Fix bugs

---

## 📋 List of UI screens to add in FE (for FE team)

Comparing the 28 backend endpoints (per SRS/YAML) with the 5 current FE pages (`POSScreen.jsx`, `CheckoutPage.jsx`, `CustomerManagement.jsx`, `OrderHistory.jsx`, `ShiftManagement.jsx`), here are **20 UI screens/components to add** to match the specs:

### A. Invoice/Payment Group (UC32-44) - 11 items to add

| # | Screen/Component to Add | UC | Description | Proposed New File |
|---|---|---|---|---|
| 1 | **ScanBarcodeModal** | UC34 | Modal to scan barcode from hardware scanner, auto-resolves productId | `components/cart/ScanBarcodeModal.jsx` |
| 2 | **ProductSearchModal** (separate) | UC35 | Modal to search product by name/SKU to add to cart (FE currently merges into POSScreen) | `components/product/ProductSearchModal.jsx` |
| 3 | **PromoCodeModal** | UC43 | Modal to enter promo code, validate + apply discount | `components/cart/PromoCodeModal.jsx` |
| 4 | **CombinedPaymentConfigModal** | UC39 | Modal to configure combined payment (cash + transfer) with breakdown of each method | `components/cart/CombinedPaymentConfigModal.jsx` |
| 5 | **DebtPaymentModal** (credit) | UC40 | Modal to record debt for regular customers/contractors, validate debtLimit + upload credit book image | `components/cart/DebtPaymentModal.jsx` |
| 6 | **QRDisplayModal** (Transfer) | UC38 | Modal displaying VietQR code for customer to scan, with 5-minute expire countdown | `components/cart/QRDisplayModal.jsx` |
| 7 | **TransferConfirmPanel** | UC38 | Panel for staff to confirm "Received money" after manually checking the bank account | `components/cart/TransferConfirmPanel.jsx` |
| 8 | **PrintPreviewModal** | UC44 | Modal to preview invoice before printing (physical or PDF) | `components/order/PrintPreviewModal.jsx` |
| 9 | **EmptyInvoiceWarning** | UC32 E3 | Toast/banner warning when user clicks "Checkout" but cart is empty (BR-73) | `components/cart/EmptyCartWarning.jsx` |
| 10 | **OutOfStockWarningModal** | UC32 E1 | Modal warning when a product is out of stock (BR-74) | `components/product/OutOfStockWarning.jsx` |
| 11 | **PriceFloorOverrideModal** | UC32 A4 E4 | Modal for authorized staff to override price below floor price (BR-75, requires special permission) | `components/cart/PriceOverrideModal.jsx` |

### B. Shift Group - 2 items to add

| # | Screen/Component to Add | UC | Description | Proposed New File |
|---|---|---|---|---|
| 12 | **StartShiftModal** | (FE has mock) | Modal to enter opening balance + select cashier | `components/shift/StartShiftModal.jsx` |
| 13 | **EndShiftModal** with variance input | (FE has mock) | Modal to enter actual physical cash counted, displays expected closing balance, calculates variance | `components/shift/EndShiftModal.jsx` |

### C. Customer/CRM Group (UC51) - 5 items to add

| # | Screen/Component to Add | UC | Description | Proposed New File |
|---|---|---|---|---|
| 14 | **CustomerOrderHistoryTab** (in CustomerDetailModal) | UC51 | Tab displaying customer's purchase history (FE currently uses fake data via `generateCustomerOrders()`) | `components/customer/CustomerOrderHistoryTab.jsx` |
| 15 | **LoyaltyPointsCard** (in CustomerDetailModal) | UC51 BR-83 | Card displaying loyalty points, membership tier, tier progress (read-only) | `components/customer/LoyaltyPointsCard.jsx` |
| 16 | **GroupFilter** (enhancement to CustomerManagement) | UC51 | Dropdown filter by group: "Individual" / "Enterprise" / "Agent" / "Contractor" | `components/customer/GroupFilter.jsx` |
| 17 | **CustomerNotesField** (enhancement to form) | UC51 | Textarea to input customer notes (FE doesn't have this field yet) | Add to CreateCustomerModal/EditCustomerModal form |
| 18 | **PhoneFormatValidator** (FE-side) | MSG-67 | Helper to validate VN phone format `^0[3|5|7|8|9][0-9]{8}$` | `utils/phoneValidator.js` |

### D. Product lookup tools Group (UC41-42) - 2 items to add

| # | Screen/Component to Add | UC | Description | Proposed New File |
|---|---|---|---|---|
| 19 | **CheckPriceTool** (button + modal) | UC41 | "Check Price" button in POSScreen, opens modal to look up retail price + floor price | `components/product/CheckPriceTool.jsx` |
| 20 | **CheckStockTool** (button + modal) | UC42 | "Check Stock" button in POSScreen, opens modal to look up cross-branch stock | `components/product/CheckStockTool.jsx` |

### E. Existing files to update (8 files)

| # | File to Update | Reason | Update Content |
|---|---|---|---|
| 21 | `endpoints.js` (POS section) | Path naming + 4 payment methods | Change from `/pos/orders` → `/pos/invoices`, add endpoints for items/scan/search/promo/payments/printing/products/price/products/stock |
| 22 | `posService.js` | Call individual endpoints | Split `createOrder` into: `createInvoice` + `addItem` + `addScan` + `addSearch` + `createPayment` + `finalizeInvoice` |
| 23 | `PaymentModal.jsx` | Support 4 methods (not 3) | Add buttons for "Debt", "QR Transfer", "Combined" |
| 24 | `usePosCart.js` | Refactor to match backend flow | Cart is local state only; call API every time an item is added/removed, do not save cart locally |
| 25 | `CustomerManagement.jsx` | Connect real API | Instead of `useState(mockCustomers)`, call `GET /pos/customers` |
| 26 | `OrderHistory.jsx` | Connect real API + filter | Call `GET /pos/invoices?date=&status=`, display correct Invoice shape |
| 27 | `posMockData.js` | Add new fields | Add `notes`, `group`, `totalSpent`, `orderCount`, `lastVisit`, `membershipTier` for mock customer |
| 28 | `ShiftManagement.jsx` | Connect real API + Start/End shift | Call `POST /pos/shifts/start`, `POST /pos/shifts/{id}/end`, `GET /pos/shifts/{id}/summary` |

### F. Proposed New Folder Structure in FE

```
src/modules/pos/
├── pages/                              (ALREADY HAS 5 pages)
│   ├── POSScreen.jsx
│   ├── CheckoutPage.jsx
│   ├── CustomerManagement.jsx
│   ├── OrderHistory.jsx
│   └── ShiftManagement.jsx
├── components/
│   ├── cart/                           (NEEDS 7 new)
│   │   ├── CartItem.jsx                 (already exists)
│   │   ├── CartSummary.jsx              (already exists)
│   │   ├── PaymentBox.jsx               (already exists)
│   │   ├── PaymentModal.jsx             (already exists - NEEDS UPDATE)
│   │   ├── PosCartPanel.jsx             (already exists)
│   │   ├── ScanBarcodeModal.jsx         (NEW - #1)
│   │   ├── ProductSearchModal.jsx       (NEW - #2)
│   │   ├── PromoCodeModal.jsx           (NEW - #3)
│   │   ├── CombinedPaymentConfigModal.jsx (NEW - #4)
│   │   ├── DebtPaymentModal.jsx         (NEW - #5)
│   │   ├── QRDisplayModal.jsx           (NEW - #6)
│   │   ├── TransferConfirmPanel.jsx     (NEW - #7)
│   │   ├── PrintPreviewModal.jsx        (NEW - #8)
│   │   ├── EmptyCartWarning.jsx         (NEW - #9)
│   │   ├── OutOfStockWarning.jsx        (NEW - #10)
│   │   └── PriceOverrideModal.jsx       (NEW - #11)
│   ├── customer/                       (NEEDS 4 new)
│   │   ├── CustomerBar.jsx              (already exists)
│   │   ├── CustomerPickerModal.jsx      (already exists)
│   │   ├── QuickAddCustomerModal.jsx     (already exists)
│   │   ├── CustomerOrderHistoryTab.jsx  (NEW - #14)
│   │   ├── LoyaltyPointsCard.jsx        (NEW - #15)
│   │   ├── GroupFilter.jsx              (NEW - #16)
│   │   └── CustomerNotesField.jsx       (NEW - #17)
│   ├── product/                        (NEEDS 2 new)
│   │   ├── BarcodeSearch.jsx            (already exists)
│   │   ├── CategoryTabs.jsx             (already exists)
│   │   ├── ProductCard.jsx              (already exists)
│   │   ├── ProductGrid.jsx              (already exists)
│   │   ├── CheckPriceTool.jsx           (NEW - #19)
│   │   └── CheckStockTool.jsx           (NEW - #20)
│   ├── shift/                          (NEEDS 2 new)
│   │   ├── StartShiftModal.jsx          (NEW - #12)
│   │   └── EndShiftModal.jsx            (NEW - #13)
│   ├── layout/                         (already exists)
│   ├── order/                          (already exists)
│   └── posMockData.js                  (UPDATE - #27)
├── hooks/                              (UPDATE)
│   ├── usePosCart.js                   (UPDATE - #24)
│   └── usePosProducts.js
├── services/
│   └── posService.js                   (UPDATE - #22)
└── utils/                              (NEEDS 1 new)
    └── phoneValidator.js               (NEW - #18)
```

### G. Summary of FE Additional Workload

| Group | Number of new screens/components | Number of updated files |
|---|---|---|
| A. Invoice/Payment | 11 new | 5 update |
| B. Shift | 2 new | - |
| C. Customer/CRM | 5 new | 3 update |
| D. Product lookup | 2 new | - |
| E. Update files | - | 8 update |
| **TOTAL** | **20 new** | **8 update** |

### H. Execution Priority (by Business Impact)

**🔴 CRITICAL (Must have before BE integrates):**
1. Update `endpoints.js` + `posService.js` (change path, add new methods)
2. Update `PaymentModal.jsx` (4 methods instead of 3)
3. Connect `OrderHistory.jsx` + `CustomerManagement.jsx` + `ShiftManagement.jsx` to real API

**🟡 HIGH (Needed for full flow):**
4. ScanBarcodeModal, ProductSearchModal
5. PromoCodeModal, DebtPaymentModal
6. PrintPreviewModal
7. StartShiftModal, EndShiftModal
8. CheckPriceTool, CheckStockTool

**🟢 MEDIUM (Polish UX):**
9. CombinedPaymentConfigModal, QRDisplayModal, TransferConfirmPanel
10. OutOfStockWarning, EmptyCartWarning, PriceOverrideModal
11. CustomerOrderHistoryTab, LoyaltyPointsCard
12. GroupFilter, CustomerNotesField, PhoneFormatValidator

**⚪ LOW (After Phase 2):**
- Return Order Management screen (UC46-50) - not in this plan

### I. Dependencies (What FE needs from BE first)

For FE to integrate, BE needs to deploy:
- `GET /api/pos/products` (direct DB query) - Day 1
- `POST /api/pos/invoices` + items + payments + finalize - Day 1-2
- `GET /api/pos/invoices` for OrderHistory - Day 2
- `GET /api/pos/customers` + create/update for CustomerManagement - Day 2
- `POST /api/pos/shifts/start` + end + summary - Day 3

→ Can divide into phases: BE works for 1 week, FE integrates gradually sprint by sprint.

---

## 🎨 J. Rules for using Shared Components (MANDATORY)

**Per FE lead's request:** all new screens/components mentioned above **MUST** use existing shared components to ensure consistent style/code, do not create custom components.

### List of Available Shared Components (from `src/shared/components/`)

| Component | Path | Purpose | When to use |
|---|---|---|---|
| **Modal** | `shared/components/Modal.jsx` | Dialog modal (header, body, footer, sizes sm→7xl) | All modals: ScanBarcodeModal, PromoCodeModal, DebtPaymentModal, QRDisplayModal, PrintPreviewModal, StartShiftModal, EndShiftModal, etc. |
| **Button** | `shared/components/Button.jsx` | Button (variants: primary/secondary/danger/success/outline, size, loading) | All buttons in new screens |
| **Input** | `shared/components/Input.jsx` | Input field (label, error, hint, icon, required) | PromoCodeInput, PhoneInput, CustomerNameInput, etc. |
| **Card** | `shared/components/Card.jsx` | Container card (header, footer, padding) | LoyaltyPointsCard, CustomerOrderHistoryTab, sections in POSScreen |
| **Table** | `shared/components/Table.jsx` | Data table (columns, loading, empty state) | CustomerOrderHistoryTab, list invoices in OrderHistory |
| **Badge** | `shared/components/Badge.jsx` | Badge/Label (variants: primary/secondary/success/warning/danger/info) | Status badges (Pending/Confirmed/Cancelled), payment method badges |
| **Icon** | `shared/components/Icon.jsx` | Icon wrapper (Material Icons → Lucide mapping) | All icons (DO NOT use emojis, DO NOT use Material CDN) |
| **IconButton** | `shared/components/IconButton.jsx` | Icon-only button | Close button, action buttons in tables |
| **Toggle** | `shared/components/Toggle.jsx` | Toggle switch | Settings, advanced options |
| **ProgressCircle** | `shared/components/ProgressCircle.jsx` | Circular progress | Loading states, loyalty tier progress |
| **FloatingActionToolbar** | `shared/components/FloatingActionToolbar.jsx` | Floating toolbar | Action shortcuts in POSScreen |
| **SpecEditorModal** | `shared/components/SpecEditorModal.jsx` | Spec editor modal | (Only use for Product Spec editor) |
| **Logo** | `shared/components/Logo.jsx` | M.E.P System Logo | Headers of screens |

### Shared Utils (from `src/shared/utils/`)

| Util | Purpose | When to use |
|---|---|---|
| **formatCurrency** | Format VND currency (1.000.000 đ) | All currency displays: prices, totals, payments, debt |
| **formatDate** | Format date DD/MM/YYYY | OrderHistory, ShiftManagement, customer.createdAt |

### Shared Hooks (from `src/shared/hooks/`)

| Hook | Purpose | When to use |
|---|---|---|
| **useAuth** | Auth state (login, logout, user, token) | Auto-attach Bearer token for every API call |
| **useDebounce** | Debounce value with custom delay | Search inputs (search product, search customer) |

### Layout Components (from `src/shared/components/layout/`)

| Component | Purpose |
|---|---|
| **Header** | Common header (search bar, notifications, user avatar) |
| **Sidebar** | Main sidebar (navigation) |
| **PrivateRoute** | Protect auth-required routes |

### ⚠️ MANDATORY RULES when coding new screens

1. **DO NOT create custom components** for Modal, Button, Input, Card, Table, Badge, Icon → **ALWAYS use shared**
2. **DO NOT use emojis** as icons → use `<Icon name="..." />` (Material Icons mapping)
3. **DO NOT use Material Icons CDN** → only via Lucide React (already mapped in `Icon.jsx`)
4. **DO NOT use large inline styles** → use Tailwind utility classes
5. **DO NOT create arbitrary colors** → use color palette in `tailwind.config.js` (primary: #004785, etc.)
6. **DO NOT manually format currency/date** → use `formatCurrency`, `formatDate`
7. **DO NOT call fetch/axios directly** → use `apiClient` from `services/apiClient.js` (auto-attaches Bearer token)

### Mapping Table: New Screens ↔ Shared Components Used

| # | New Screen | Shared Components Used |
|---|---|---|
| 1 | ScanBarcodeModal | `Modal` (size=md), `Icon` (camera), `Input` (manual barcode entry) |
| 2 | ProductSearchModal | `Modal` (size=xl), `Input` (search with `useDebounce`), `Table` (results), `Button` |
| 3 | PromoCodeModal | `Modal` (size=sm), `Input` (code), `Button` (apply), `Badge` (status) |
| 4 | CombinedPaymentConfigModal | `Modal` (size=lg), `Input` (amount), `Button` (add line), `Card` (breakdown) |
| 5 | DebtPaymentModal | `Modal` (size=md), `Input` (amount), `Card` (debt info), `Button`, `Badge` |
| 6 | QRDisplayModal | `Modal` (size=md), `Icon` (qr), `Card` (countdown), `Button` (close) |
| 7 | TransferConfirmPanel | `Card`, `Button` (confirm), `Badge` (status) |
| 8 | PrintPreviewModal | `Modal` (size=xl), `Button` (print, download) |
| 9 | EmptyCartWarning | `Modal` (size=sm), `Icon` (warning), `Button` |
| 10 | OutOfStockWarning | `Modal` (size=sm), `Icon` (error), `Button` |
| 11 | PriceOverrideModal | `Modal` (size=sm), `Input` (price), `Button` (request approval) |
| 12 | StartShiftModal | `Modal` (size=md), `Input` (openingBalance), `Button` |
| 13 | EndShiftModal | `Modal` (size=lg), `Input` (actualCashCount), `Card` (summary), `Button` |
| 14 | CustomerOrderHistoryTab | `Table` (orders), `Badge` (status), `formatCurrency`, `formatDate` |
| 15 | LoyaltyPointsCard | `Card`, `ProgressCircle` (tier progress), `Badge` (tier name) |
| 16 | GroupFilter | `Input` (dropdown) or `Toggle` (segmented), `Button` |
| 17 | CustomerNotesField | `Input` (textarea variant) |
| 18 | PhoneFormatValidator | Pure util (regex), used with `Input` displaying error message |
| 19 | CheckPriceTool | `Modal` (size=sm), `Input` (search), `Card` (price info), `formatCurrency` |
| 20 | CheckStockTool | `Modal` (size=md), `Input` (search), `Table` (stock per branch) |

### Updates for 8 Existing Files

| # | File to update | Additional Shared Components to use |
|---|---|---|
| 21 | `endpoints.js` | (No change - just add URLs) |
| 22 | `posService.js` | (No change) |
| 23 | `PaymentModal.jsx` | **UPDATE**: replace emojis (💵💳📱) with `<Icon name="..." />`, add buttons "Debt", "QR Transfer", "Combined" - use `Button` |
| 24 | `usePosCart.js` | (Hook logic, no shared UI needed) |
| 25 | `CustomerManagement.jsx` | **UPDATE**: use `Table` instead of custom table, `Input` for search, `Modal` for create/edit, `Badge` for status |
| 26 | `OrderHistory.jsx` | **UPDATE**: use `Table`, `Badge` (status), `formatCurrency`, `formatDate`, `Button` to view details |
| 27 | `posMockData.js` | (Mock data, no shared UI needed) |
| 28 | `ShiftManagement.jsx` | **UPDATE**: use `Modal` for Start/End, `Table` for list, `Input` for openingBalance, `formatCurrency` |

### Code Template Example (for a new screen)

```jsx
// src/modules/pos/components/cart/PromoCodeModal.jsx
import { useState } from 'react';
import { Modal } from '../../../../shared/components/Modal';
import { Button } from '../../../../shared/components/Button';
import { Input } from '../../../../shared/components/Input';
import { Badge } from '../../../../shared/components/Badge';
import { Icon } from '../../../../shared/components/Icon';
import { useDebounce } from '../../../../shared/hooks/useDebounce';
import { formatCurrency } from '../../../../shared/utils/formatCurrency';

const PromoCodeModal = ({ isOpen, onClose, subtotal, onApply }) => {
  const [code, setCode] = useState('');
  const [validating, setValidating] = useState(false);
  const [result, setResult] = useState(null);
  const debouncedCode = useDebounce(code, 500);

  // ... validate logic ...

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Apply Promotion Code"
      size="md"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button variant="primary" onClick={onApply} loading={validating}>
            Apply
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <Input
          label="Promo Code"
          value={code}
          onChange={(e) => setCode(e.target.value.toUpperCase())}
          placeholder="EX: SUMMER2026"
          icon={<Icon name="local_offer" size={16} />}
        />
        {result && (
          <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-3">
            <div className="flex items-center gap-2">
              <Icon name="check_circle" size={16} className="text-emerald-600" />
              <span className="text-sm font-bold text-emerald-700">
                Discount {formatCurrency(result.discountAmount)}
              </span>
            </div>
            <p className="mt-1 text-xs text-slate-600">{result.description}</p>
          </div>
        )}
      </div>
    </Modal>
  );
};

export default PromoCodeModal;
```

**Note**: DO NOT copy/paste from the existing FE mock (`PaymentModal.jsx`) - refactor using shared components to ensure consistency.

---

## Comparison with FE mock (POS-API.md)

FE is currently using 18 mocked endpoints with a different format (merging into `/orders` instead of separate `/invoices`, 3 payment methods). This plan implements **per the SRS spec** (28 endpoints) to be accurate to the design. When FE switches to using real APIs, it will need to:

1. Update `endpoints.js` to call the correct paths
2. Update `posService.js` to call individual endpoints instead of merged ones
3. Update `PaymentModal.jsx` to support 4 methods (Cash/Transfer/Combined/Debt) instead of 3
4. Update `CustomerPickerModal` and `posMockData.js` to match the response shape

→ BE implements according to SRS, FE will self-adapt. An adapter can be added if necessary.
