# Admin Site APIs Analysis (based on SRS Report 3 section 1.4.1.1)

> **Purpose**: This analysis is created to serve as an input for planning & implementing **MEP.Admin.Api** (ASP.NET Core Web API) for the Admin site of the M.E.P Management System project.
>
> **Reference Sources**:
> - SRS docx: `SEP490_G51_APHL_Report3_SRS.docx` (section 1.4.1.1 Administrator Site)
> - Current DB models: `MEP.Data/Models/` (25 entities based on SRS already exist)
> - Complete seed data already exists: 12 users, 30 products, 10 customers, 8 posts, 5 orders, 5 invoices, 3 POs, ...
> - Frontend React (partially exists): `Metal-ERP/src/modules/admin/` with 4 pages: AdminDashboard, UserManagement, MasterData, Moderation

---

## Overall Scope

According to the SRS, the Admin Site has **5 main functional groups** with **21 screens**. We need to implement **~35 API endpoints** across **9 controllers**, along with **2 new tables** (`SystemNotification`, `StoreApproval`) + **1 new field** (`User.IsVerified`) + **2 new fields** for `CommunityCategory` (`IsArchived`, `SortOrder`).

### Comparison: SRS vs Current FE

| SRS Screen | Exists in FE? | Needs FE addition? |
|---|---|---|
| 1. Admin Dashboard | ✅ Yes (AdminDashboard.jsx) | No |
| 2. Account Management - Staff | ❌ (UserManagement.jsx is just 1 tab) | Yes - split into Staff/Community |
| 3. Account Management - Community User | ❌ | Yes - split into Staff/Community |
| 4. Post Moderator + Pin/Lock | ❌ (Moderation.jsx currently only handles reports) | Yes - add Post Moderator screen |
| 5. Violation Report Panel | ✅ Yes (Moderation.jsx) | No |
| 6. Category Management | ❌ (MasterData.jsx only has material categories tree) | Yes - needs separate screen for community categories |
| 7. System Logs viewer | ❌ | Yes - new screen |
| 8. Compose System Notification | ❌ (MasterData.jsx has broadcasts section but incomplete) | Yes - new screen |
| 9. Store Approval workflow | ❌ (UserManagement.jsx has APPROVE button but no detailed flow) | Yes - new screen |

---

## 1. Admin Dashboard (based on Use Case 2.13)

| # | Screen | API to Implement |
|---|---|---|
| 1 | Admin Dashboard | `GET /api/admin/dashboard/stats` |
|   |                   | `GET /api/admin/dashboard/revenue-chart?year=2026` |
|   |                   | `GET /api/admin/dashboard/recent-events?limit=20` |
|   |                   | `GET /api/admin/dashboard/export` (CSV) |

**Return fields for `/stats`**:
- `activeTenants` (number of Owner roles currently IsActive=1)
- `subscriptionRevenue` (total TotalAmount of COMPLETED Invoices in the last 30 days)
- `b2bCommission` (1% * total Orders with PaymentMethod=TRANSFER)
- `overdueAlerts` (number of Customers with CurrentDebt > DebtLimit)

---

## 2. Account Management - Staff (screens 2, 3, 4)

| # | SRS Screen | API to Implement |
|---|---|---|
| 2 | **Staff Account Tab** | `GET /api/admin/staff?search=&status=&page=&pageSize=` |
| 3 | **Change Status/Ban Staff Modal** | `PUT /api/admin/staff/{id}/status` (body: `{status: "active"|"suspended"}`) |
|   |                                              | `PUT /api/admin/staff/{id}/ban` (body: `{reason: "..."}`) |
| 4 | **Edit Staff Info Panel** | `GET /api/admin/staff/{id}` |
|   |                                  | `PUT /api/admin/staff/{id}` (body: fullname, email, phone, role) |

**Filters for `/staff`**: role=Owner/SalesStaff/InventoryStaff, search by FullName/Email

---

## 3. Account Management - Community User (screens 5, 6, 7)

| # | SRS Screen | API to Implement |
|---|---|---|
| 5 | **Community User Account Management** | `GET /api/admin/community-users?search=&status=&page=&pageSize=` |
| 6 | **Change Status/Ban User Modal** | `PUT /api/admin/community-users/{id}/status` |
|   |                                                    | `PUT /api/admin/community-users/{id}/ban` |
| 7 | **Edit User Info Panel** | `GET /api/admin/community-users/{id}` |
|   |                                            | `PUT /api/admin/community-users/{id}` |

**Special field**: `isVerified` (User.IsVerified - new field to be added)

---

## 4. Content Moderation - Posts (screens 8, 9, 10)

| # | SRS Screen | API to Implement |
|---|---|---|
| 8  | **Content Moderator Management** (container) | (UI shell - no separate API needed) |
| 9  | **Post Moderator Screen** | `GET /api/admin/posts?status=&search=&page=&pageSize=` |
| 10 | **Pin/Lock Thread Modal** | `PUT /api/admin/posts/{id}/lock` |
|   |                                       | `PUT /api/admin/posts/{id}/unlock` |
|   |                                       | `PUT /api/admin/posts/{id}/pin` |
|   |                                       | `PUT /api/admin/posts/{id}/unpin` |
|   |                                       | `PUT /api/admin/posts/{id}/hide` (soft delete) |

**Filters for `/posts`**: status=ACTIVE/LOCKED/HIDDEN, search by Title

---

## 5. Content Moderation - Violation Reports (screen 11)

| # | SRS Screen | API to Implement |
|---|---|---|
| 11 | **Violation Report Panel** | `GET /api/admin/violation-reports?status=&page=&pageSize=` |
|    |                                          | `GET /api/admin/violation-reports/{id}` (full detail) |
|    |                                          | `PUT /api/admin/violation-reports/{id}/resolve` (body: `{adminNote, action: "hide_post"|"hide_comment"|"dismiss"}`) |

**Side effect upon resolving**: automatically set `CommunityPost.Status = "HIDDEN"` or `Comment.IsDeleted = true` depending on the action

---

## 6. Content Moderation - Categories (screen 12)

| # | SRS Screen | API to Implement |
|---|---|---|
| 12 | **Category Management Screen** | `GET /api/admin/community-categories` |
|    |                                              | `POST /api/admin/community-categories` (body: name, description, sortOrder) |
|    |                                              | `PUT /api/admin/community-categories/{id}` |
|    |                                              | `DELETE /api/admin/community-categories/{id}` (soft delete = set IsArchived=true) |
|    |                                              | `PUT /api/admin/community-categories/{id}/reorder` (body: `{newOrder: int}`) |

**Fields to add** to `CommunityCategory`:
- `IsArchived` (bool, default false)
- `SortOrder` (int, default 0)

---

## 7. System Management - Notifications (screens 13, 14)

| # | SRS Screen | API to Implement |
|---|---|---|
| 13 | **System Management Screen** (gateway) | (UI shell) |
| 14 | **Compose System Notification Screen** | `GET /api/admin/notifications?status=&page=` |
|    |                                                      | `POST /api/admin/notifications` (body: title, content, target, scheduledAt) |
|    |                                                      | `PUT /api/admin/notifications/{id}` (edit draft) |
|    |                                                      | `PUT /api/admin/notifications/{id}/send` (immediate send) |
|    |                                                      | `PUT /api/admin/notifications/{id}/cancel` |
|    |                                                      | `DELETE /api/admin/notifications/{id}` |

**Need to create new table `SystemNotification`**:
```csharp
public class SystemNotification {
    public Guid NotificationId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string Target { get; set; }    // "ALL" / "OWNER" / "STAFF" / "COMMUNITY"
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string Status { get; set; }    // "DRAFT" / "SCHEDULED" / "SENT" / "CANCELLED"
    public Guid CreatedBy { get; set; }   // Admin user FK
    public DateTime CreatedAt { get; set; }
}
```

---

## 8. System Management - Logs (screens 15, 16)

| # | SRS Screen | API to Implement |
|---|---|---|
| 15 | **System Logs Screen** | `GET /api/admin/system-logs?action=&userId=&fromDate=&toDate=&page=&pageSize=` |
| 16 | **Log Detail & Error Trace Panel** | `GET /api/admin/system-logs/{id}` (full detail with stack trace) |
|    |                                          | `GET /api/admin/system-logs/export` (CSV) |

**`SystemLog` table already exists**, just need API queries with advanced filtering.

---

## 9. Store Approval (screens 17, 18, 19)

| # | SRS Screen | API to Implement |
|---|---|---|
| 17 | **Owner Approval Screen** | `GET /api/admin/store-approvals?status=PENDING&page=&pageSize=` |
| 18 | **Store Detail & Document Panel** | `GET /api/admin/store-approvals/{id}` (documents uploaded) |
| 19 | **Approve/Reject Store Modal** | `PUT /api/admin/store-approvals/{id}/approve` (body: `{branchId, notes}`) |
|    |                                                    | `PUT /api/admin/store-approvals/{id}/reject` (body: `{reason}`) |

**Need to create new table `StoreApproval`**:
```csharp
public class StoreApproval {
    public Guid ApprovalId { get; set; }
    public Guid UserId { get; set; }       // Owner applicant FK
    public string StoreName { get; set; }
    public string? BusinessLicense { get; set; }  // Business Registration Code
    public string? TaxCode { get; set; }
    public string? StoreAddress { get; set; }
    public string? DocumentUrls { get; set; }     // JSON array of file URLs
    public string Status { get; set; }     // "PENDING" / "APPROVED" / "REJECTED"
    public string? RejectReason { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Side effect upon approval**:
- Set `User.IsVerified = true`
- Create new `Branch` for the store
- Create `StaffAssignment` assigning User → Branch
- Log `SystemLog`

---

## Implementation Summary

**Files to create** (`MEP.Admin.Api/`):
```
MEP.Admin.Api/
├── MEP.Admin.Api.csproj
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── Properties/launchSettings.json
├── Controllers/
│   ├── AdminDashboardController.cs          (4 endpoints)
│   ├── StaffAccountsController.cs           (4 endpoints)
│   ├── CommunityUsersController.cs          (4 endpoints)
│   ├── PostsModerationController.cs         (6 endpoints)
│   ├── ViolationReportsController.cs        (3 endpoints)
│   ├── CommunityCategoriesController.cs     (5 endpoints)
│   ├── SystemNotificationsController.cs     (6 endpoints)
│   ├── SystemLogsController.cs              (3 endpoints)
│   └── StoreApprovalsController.cs          (4 endpoints)
├── DTOs/
│   ├── DashboardStatsDto.cs
│   ├── RevenueChartPointDto.cs
│   ├── SystemEventDto.cs
│   ├── StaffAccountDto.cs
│   ├── CommunityUserDto.cs
│   ├── PostDto.cs
│   ├── ViolationReportDto.cs
│   ├── CommunityCategoryDto.cs
│   ├── SystemNotificationDto.cs
│   ├── SystemLogDto.cs
│   ├── StoreApprovalDto.cs
│   └── PageResultDto.cs
└── AutoMapper/
    └── AdminMappingProfile.cs
```

**Files to update** (`MEP.Data/`):
```
MEP.Data/
├── Models/
│   ├── User.cs                    (+IsVerified field)
│   ├── CommunityCategory.cs       (+IsArchived, SortOrder fields)
│   ├── SystemNotification.cs      (NEW)
│   ├── StoreApproval.cs           (NEW)
│   └── MEPDatabaseContext.cs      (+DbSet, fluent API, relationships)
├── Seed/
│   └── DatabaseSeeder.cs          (+broadcasts, store approvals, update IsVerified)
└── Migrations/
    └── YYYYMMDDHHMMSS_AddAdminEntities.cs  (NEW - auto-generate via dotnet ef)
```

**Minor updates**:
- `M.E.P Management System.sln` (add MEP.Admin.Api project)

---

## Pattern Grounding (Verified from current codebase)

| Category | Source | Pattern |
|---|---|---|
| Project structure | `MEP.Inventory.Api/Program.cs` | ASP.NET Core 8 Web API + JWT Bearer + Swagger + Npgsql |
| JWT config | `MEP.Inventory.Api/Program.cs:16-34` | `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Key` from appsettings |
| Authorization | `ProductsController.cs:14` | `[Authorize]` + `[Authorize(Roles = "Admin")]` |
| Get user branchId from JWT | `ProductsController.cs:29-37` | `User.FindFirst("branchId")?.Value` |
| AutoMapper | `MEP.Auth.Api/Program.cs:53` | `AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies())` |
| OData filter | `MEP.Auth.Api/Program.cs:29` | `AddOData(options => options.Select().Filter().OrderBy().Count().SetMaxTop(100))` |
| JSON cycle | `MEP.Inventory.Api/Program.cs:37-41` | `ReferenceHandler.IgnoreCycles` |
| Controller naming | ProductsController, AuthController | `[ApiController] [Route("api/[controller]")]` |
| Database access | `ProductsController.cs:49-53` | `_context.BranchProducts.Include(...).Where(...).AsQueryable()` |
| Pagination | `ProductDto.cs:47-54` | `PageNumber`, `PageSize`, `SearchTerm` parameter object |

---

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Old schema conflict when adding IsVerified + Notification + StoreApproval | Medium | High | Drop schema `public` and re-run migration |
| Endpoint shape mismatch with FE mockData.js | High | High | Test thoroughly each endpoint with curl + compare JSON fields |
| Auth.Api and Admin.Api using different JWT secrets | Low | High | Copy `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Key` exactly from Inventory.Api |
| CORS blocking FE calls | High | Medium | Add `AddCors()` with policy allowing `http://localhost:3000` |
| Complex LINQ queries causing N+1 | Medium | Medium | Use `.Include()` + `.Select()` projection instead of navigation properties |
| Store Approval needs to handle both new Owner registration (via Auth API) and manual Admin creation | Medium | Medium | Add StoreApproval table, modify AuthController.RegisterVerify to create a StoreApproval record when a User registers with Owner role |
| 2 API projects running simultaneously (Auth 7001 + Admin 7003) causing port conflict | Medium | Low | Set ports explicitly in launchSettings.json |

---

## Estimated Complexity: **MEDIUM-LARGE** (35 endpoints, 2 new tables, 9 controllers)

- Foundation + Project setup: 1-2 hours
- Data layer (2 new tables + 1 migration): 1-2 hours
- DTOs + AutoMapper: 1-2 hours
- 9 Controllers × ~30-50 lines: 4-6 hours
- Additional seed data + integration testing: 1-2 hours
- Debug + fixing: 1-2 hours
- **Total: 9-14 hours** (~1.5-2 work days)

---

## Acceptance Criteria

- [ ] `dotnet build` entire solution passes with no errors
- [ ] `MEP.Admin.Api` runs successfully, opening `https://localhost:7003/swagger` shows all endpoints
- [ ] Login with `admin@mep.vn` / `MEP@2026` succeeds, returns token
- [ ] `GET /api/admin/dashboard/stats` returns 200 with 4 fields
- [ ] `GET /api/admin/staff?status=active` returns list of staff
- [ ] `PUT /api/admin/staff/{id}/status` sets IsActive correctly
- [ ] `GET /api/admin/posts?status=ACTIVE` returns list of posts
- [ ] `PUT /api/admin/posts/{id}/lock` sets post status to LOCKED
- [ ] `GET /api/admin/violation-reports?status=PENDING` returns list of reports
- [ ] `PUT /api/admin/violation-reports/{id}/resolve` sets status + hides post
- [ ] `GET /api/admin/community-categories` returns list
- [ ] `POST /api/admin/notifications` creates a notification
- [ ] `PUT /api/admin/notifications/{id}/send` sets status to SENT
- [ ] `GET /api/admin/system-logs?action=APPROVE_PO` returns list of logs
- [ ] `GET /api/admin/store-approvals?status=PENDING` returns list
- [ ] `PUT /api/admin/store-approvals/{id}/approve` sets status + IsVerified
- [ ] CORS allows React FE (port 3000) to make calls
- [ ] JSON response shapes match `MOCK_TENANTS`, `MOCK_REPORTS`, `MOCK_CATEGORIES` in FE's `mockData.js`

---

## Out of Scope (According to SRS, but belongs to other actors)

The following screens belong to **Owner / SalesStaff / InventoryStaff** (not Admin), and will be implemented in separate APIs for each actor:

- **Owner**: Owner Dashboard, Owner Finance (AP/AR), Revenue Report, Branch Performance, Cash Flow, Petty Cash, System Config, Role Permission, Staff Assignment, CRM, Customer Credit Limit
- **SalesStaff**: POS, Barcode Scanner, Customer Quick Create, Payment Process, Invoice Print, Promotion, Order History
- **InventoryStaff**: Inventory Dashboard, Stock Intake, Return to Supplier, Stock Check, Stock Transfer, Stock Monitoring, Warehouse Report, Branch Price Management
- **CommunityUser**: View Posts, Comment, Post Supply Source, Group Buying, Notifications
