using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Hubs;
using MEP_Management_System.Middleware;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Services.Email;
using MEP_Management_System.Services.InventoryService;
using MEP_Management_System.Services.InventoryServices;
using InventoryServices = MEP_Management_System.Services.InventoryServices;
using MEP_Management_System.Services.UserServices;
using MEP_Management_System.Services.AccountSettingsServices;
using MEP_Management_System.Services.SaleServices;
using MEP_Management_System.Services.OwnerServices;
using MEP_Management_System.Services.AdminAccountServices;
using MEP_Management_System.Services.AdminDashboardServices;
using MEP_Management_System.Services.AdminBranchServices;
using MEP_Management_System.Services.AdminRoleServices;
using MEP_Management_System.Services.AdminUserServices;
using MEP_Management_System.Services.AuthServices;
using MEP_Management_System.Services.ChatbotServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OData.ModelBuilder;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Fix: PostgreSQL timestamp type mismatch — allow UTC Kind=UTC to write to timestamp without time zone columns
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddDbContext<MEPDatabaseContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// Services
builder.Services.AddSingleton<JwtHelper>();
builder.Services.AddScoped<INotificationSender, SignalRNotificationSender>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentBranchAccessor, CurrentBranchAccessor>();
builder.Services.AddScoped<IOrderDiscountService, OrderDiscountService>();
builder.Services.AddScoped<ISaleInvoiceService, SaleInvoiceService>();
builder.Services.AddScoped<IInwardInventoryExcelService, InwardInventoryExcelService>();
builder.Services.AddScoped<IReportService, ReportService>();
//builder.Services.AddScoped<IProductService, ProductService>(); // Removed: using static ProductService methods directly
//builder.Services.AddScoped<IInvoiceService, InvoiceService>(); // Removed: InvoiceService is now static
builder.Services.AddScoped<ISupplierDebtService, SupplierDebtService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<ISupplierPaymentService, SupplierPaymentService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IProductTaxonomyService, ProductTaxonomyService>();
builder.Services.AddScoped<InventoryLedgerService>();
builder.Services.AddScoped<CustomerDebtService>();
builder.Services.AddScoped<InwardInventoryQueryService>();
builder.Services.AddScoped<IProductService, ProductServiceImpl>();
builder.Services.AddScoped<IInwardInventoryService, InwardInventoryServiceImpl>();
builder.Services.AddScoped<IOutwardInventoryService, OutwardInventoryService>();
builder.Services.AddScoped<IInventoryCheckService, InventoryCheckServiceImpl>();
builder.Services.AddScoped<InventoryServices.IOutwardInventoryExcelService, InventoryServices.OutwardInventoryExcelServiceImpl>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IStockImportService, StockImportService>();
builder.Services.AddScoped<IStoreApprovalService, StoreApprovalService>();
builder.Services.AddScoped<IGoodsIssueService, GoodsIssueServiceImpl>();
builder.Services.AddScoped<IStockPrintTemplateService, StockPrintTemplateService>();
// builder.Services.AddScoped<InvoicePrintTemplateService>(); // Now using InvoicePrintTemplateServiceImpl via IInvoicePrintTemplateService
//builder.Services.AddScoped<MEP_Management_System.Services.Chatbot.ToolDispatcher>(); // Removed: ToolDispatcher not found
builder.Services.AddScoped<MEP_Management_System.Services.Chatbot.SqlExecutor>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAccountSettingsService, AccountSettingsService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICustomerPaymentService, CustomerPaymentService>();
builder.Services.AddScoped<IPOSProductService, POSProductService>();
builder.Services.AddScoped<IReturnService, ReturnServiceImpl>();
builder.Services.AddScoped<IOrderDiscountTierService, OrderDiscountTierService>();
builder.Services.AddScoped<IOwnerAccountService, OwnerAccountService>();
builder.Services.AddScoped<IOwnerBranchService, OwnerBranchService>();
builder.Services.AddScoped<IOwnerOrderDiscountTierService, OwnerOrderDiscountTierService>();
builder.Services.AddScoped<IOwnerDashboardService, OwnerDashboardService>();
builder.Services.AddScoped<IOwnerAuditLogService, OwnerAuditLogService>();
builder.Services.AddScoped<IOwnerStaffService, OwnerStaffService>();
builder.Services.AddScoped<IInvoicePrintTemplateService, InvoicePrintTemplateServiceImpl>();
builder.Services.AddScoped<IBranchResolutionService, BranchResolutionService>();
builder.Services.AddScoped<IExpenseService, ExpenseServiceImpl>();
builder.Services.AddScoped<ISystemLogService, SystemLogService>();
builder.Services.AddScoped<ISystemNotificationService, SystemNotificationService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<IWarrantyService, WarrantyService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPrintingService, PrintingService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddSingleton<MEP_Management_System.Services.Chatbot.ChatbotRateLimiter>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailService, EmailService>();

// Admin Services
builder.Services.AddScoped<IAdminAccountService, AdminAccountService>();
builder.Services.AddScoped<MEP_Management_System.Services.AdminDashboardServices.IAdminDashboardService, MEP_Management_System.Services.AdminDashboardServices.AdminDashboardService>();
builder.Services.AddScoped<MEP_Management_System.Services.AdminBranchServices.IAdminBranchService, MEP_Management_System.Services.AdminBranchServices.AdminBranchService>();
builder.Services.AddScoped<MEP_Management_System.Services.AdminRoleServices.IAdminRoleService, MEP_Management_System.Services.AdminRoleServices.AdminRoleService>();
builder.Services.AddScoped<MEP_Management_System.Services.AdminUserServices.IAdminUserService, MEP_Management_System.Services.AdminUserServices.AdminUserService>();

// Background Services
builder.Services.AddHostedService<UserCleanupService>();
builder.Services.AddHostedService<NotificationDispatcherService>();
// OData
var odataBuilder = new ODataConventionModelBuilder();
odataBuilder.EntitySet<User>("Users");
odataBuilder.EntitySet<Role>("Roles");
//var postsCfg = odataBuilder.EntitySet<CommunityPost>("CommunityPosts");
//postsCfg.EntityType.HasKey(p => p.PostId);
//var catsCfg = odataBuilder.EntitySet<CommunityCategory>("CommunityCategories");
//catsCfg.EntityType.HasKey(c => c.CategoryId);
var logsCfg = odataBuilder.EntitySet<SystemLog>("SystemLogs");
logsCfg.EntityType.HasKey(l => l.LogId);
var notifCfg = odataBuilder.EntitySet<SystemNotification>("SystemNotifications");
notifCfg.EntityType.HasKey(n => n.NotificationId);

// Register VietQR Service
builder.Services.Configure<VietQrOptions>(builder.Configuration.GetSection("VietQr"));
builder.Services.AddHttpClient<VietQrService>();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .SelectMany(e => e.Value!.Errors.Select(x =>
                {
                    var fieldName = e.Key.Split('.').Last();
                    var msg = x.ErrorMessage;
                    if (string.IsNullOrEmpty(msg) && x.Exception != null)
                        msg = x.Exception.Message;
                    if (string.IsNullOrEmpty(msg))
                        msg = $"Trường '{fieldName}' không hợp lệ.";
                    else if (msg.Contains("is required"))
                        msg = $"Trường '{fieldName}' là bắt buộc.";
                    else if (msg.Contains("Could not convert") || msg.Contains("could not convert"))
                        msg = $"Trường '{fieldName}' không đúng định dạng.";
                    else if (msg.Contains("The JSON value"))
                        msg = $"Trường '{fieldName}' không đúng định dạng.";
                    return msg;
                }));

            return new BadRequestObjectResult(new
            {
                success = false,
                message = "Dữ liệu không hợp lệ.",
                errors = errors.Any() ? errors.ToArray() : new[] { "Dữ liệu gửi lên không đúng định dạng." }
            });
        };
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
    })
    .AddOData(options =>
    options.Select().Filter().OrderBy().Count().SetMaxTop(100)
           .AddRouteComponents("odata", odataBuilder.GetEdmModel()));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ClockSkew = TimeSpan.Zero
    };
    // Allow SignalR clients to send JWT via ?access_token= query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/r/inventoryHub") ||
                 path.StartsWithSegments("/r/mepHub") ||
                 path.StartsWithSegments("/r/posHub")))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// =========================================================================
// RBAC: đăng ký Authorization Handler + toàn bộ Policy Permission
// =========================================================================
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    PermissionPolicyFactory.RegisterAll(options);
});

builder.Services.AddScoped<IInventoryReportService, InventoryReportService>();
builder.Services.AddScoped<MepNotificationService>();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://mep-retail-management-system.vercel.app"
            )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MEP Admin API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste your JWT token here (no need to type Bearer)."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":{\"code\":\"MSG-99\",\"message\":\"Đã xảy ra lỗi.\"}}");
        });
    });
}

app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MEP API V1");

    c.HeadContent = @"
        <script>
            window.addEventListener('load', function() {
                setTimeout(function() {
                    // Lấy instance của Swagger UI đang chạy trên trang
                    var ui = window.ui;
                    if (ui) {
                        var token = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjIwMDAwMDAwLTAwMDAtMDAwMC0wMDAwLTAwMDAwMDAwMDAwMiIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL25hbWUiOiJvd25lci5iYWNAbWVwLnZuIiwiaHR0cDovL3NjaGVtYXMueG1sc29hcC5vcmcvd3MvMjAwNS8wNS9pZGVudGl0eS9jbGFpbXMvZ2l2ZW5uYW1lIjoiVHLhuqduIFbEg24gQuG6r2MgKE93bmVyKSIsImJyYW5jaElkIjoiMzAwMDAwMDAtMDAwMC0wMDAwLTAwMDAtMDAwMDAwMDAwMDAxIiwiaHR0cDovL3NjaGVtYXMubWljcm9zb2Z0LmNvbS93cy8yMDA4LzA2L2lkZW50aXR5L2NsYWltcy9yb2xlIjoiT3duZXIiLCJleHAiOjE4MTU0ODczNDYsImlzcyI6IkludmVudG9yeVNlcnZpY2VBUEkiLCJhdWQiOiJJbnZlbnRvcnlTZXJ2aWNlVXNlcnMifQ.Pxtw-X7Eb0-N9cPZmFMzAAu3aMYVP_ltkim4uDh5_WI';
                        
                        // Thực hiện gán token trực tiếp vào cấu hình Auth của Swagger UI
                        ui.preauthorizeApiKey('Bearer', token);
                        
                        console.log('--- [MEP System] Swagger đã tự động Đăng nhập tài khoản Owner thành công! ---');
                    } else {
                        console.error('--- [MEP System] Không tìm thấy Swagger UI instance để tự động đăng nhập. ---');
                    }
                }, 1000); // Giảm xuống 1 giây (1000ms) để chạy ngay lập tức khi trang load xong
            });
        </script>
    ";
});

app.UseCors("AllowFrontend");
// app.UseHttpsRedirection(); // Bỏ comment khi deploy lên server thật có SSL/TLS
app.UseAuthentication();
app.UseGlobalExceptionHandler(); // Bắt tất cả exceptions, trả về ApiResponse chuẩn
app.UseActiveUserCheck();
app.UseAuthorization();

app.MapControllers();
app.MapHub<InventoryHub>("/r/inventoryHub");
app.MapHub<MepHub>("/r/mepHub");
app.MapHub<PosHub>("/r/posHub");

// Seed database on startup (only seeds if DB is empty)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MEPDatabaseContext>();
    await MEP_Management_System.Seed.DatabaseSeeder.SeedAsync(context);
}

// Đảm bảo bảng product_taxonomies tồn tại (cho nhóm hàng & thương hiệu)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MEPDatabaseContext>();
    try
    {
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS product_taxonomies (
                taxonomy_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                name VARCHAR(255) NOT NULL,
                type VARCHAR(50) NOT NULL CHECK (type IN ('Category', 'Brand')),
                created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(name, type)
            );
            CREATE INDEX IF NOT EXISTS idx_product_taxonomies_type ON product_taxonomies(type);
        ");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Tạo bảng product_taxonomies thất bại: {ex.Message}");
    }
}

// Đảm bảo cột media_url có thể lưu base64 images (mặc định VARCHAR(255) là quá nhỏ)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MEPDatabaseContext>();
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE media_resources ALTER COLUMN media_url TYPE TEXT;"
        );
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Alter media_url thất bại: {ex.Message}");
    }
}

// Cho phép RefundRecord.customer_id NULL — đơn trả hàng cho khách lẻ (Order.CustomerId == null)
// không có Customer thật. Trước đây gán Guid.Empty → vi phạm FK refund_records_customer_id_fkey → HTTP 500.
// DROP NOT NULL là idempotent (an toàn khi chạy lại trên cột đã nullable).
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MEPDatabaseContext>();
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE refund_records ALTER COLUMN customer_id DROP NOT NULL;"
        );
        Console.WriteLine("[Startup] refund_records.customer_id -> nullable OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Alter refund_records.customer_id thất bại: {ex.Message}");
    }
}

// Mở rộng inventory_ledger.doc_type lên varchar(50) — giá trị "CUSTOMER_RETURN_DEFECTIVE" (25 ký tự)
// tràn cột varchar(20) cũ → lỗi 22001 khi hoàn tiền đơn trả có line DEFECTIVE. ALTER TYPE idempotent.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MEPDatabaseContext>();
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE inventory_ledger ALTER COLUMN doc_type TYPE VARCHAR(50);"
        );
        Console.WriteLine("[Startup] inventory_ledger.doc_type -> varchar(50) OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Alter inventory_ledger.doc_type thất bại: {ex.Message}");
    }
}

// Migration 1-lần: chuyển unit_price của SALE StockTicketItem CŨ từ GIÁ GỐC (SellPrice) sang GIÁ NET
// (sau chiết khấu đơn hàng). Áp dụng cho các SALE ticket tạo TRƯỚC fix Task 9 (InvoicesController:610).
// Dùng system_configs làm sentinel để idempotent — chạy lại không scale lần thứ 2.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MEPDatabaseContext>();
    try
    {
        var rows = await context.Database.ExecuteSqlRawAsync(@"
UPDATE stock_ticket_items sti
SET unit_price = ROUND(sti.unit_price * (inv.total_amount / NULLIF(inv.sub_total_amount, 0)), 4)
FROM stock_tickets st, invoices inv
WHERE sti.stock_ticket_id = st.stock_ticket_id
  AND inv.invoice_code = st.ticket_code
  AND st.ticket_type = 'SALE'
  AND COALESCE(inv.sub_total_amount, 0) > 0
  AND inv.total_amount < inv.sub_total_amount
  AND NOT EXISTS (SELECT 1 FROM system_configs WHERE config_key = 'SALE_NET_PRICE_MIGRATION_V1');
");
        Console.WriteLine($"[Startup] SALE net-price migration: {rows} dòng đã scale sang giá NET.");
        await context.Database.ExecuteSqlRawAsync(@"
INSERT INTO system_configs (config_id, branch_id, config_key, config_value)
SELECT gen_random_uuid(), NULL, 'SALE_NET_PRICE_MIGRATION_V1', 'done'
WHERE NOT EXISTS (SELECT 1 FROM system_configs WHERE config_key = 'SALE_NET_PRICE_MIGRATION_V1');
");
        Console.WriteLine("[Startup] SALE net-price migration sentinel OK.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] SALE net-price migration thất bại: {ex.Message}");
    }
}


// Đảm bảo cột notes và unit_name tồn tại trong stock_ticket_items
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MEPDatabaseContext>();
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE stock_ticket_items ADD COLUMN IF NOT EXISTS notes TEXT;"
        );
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE stock_ticket_items ADD COLUMN IF NOT EXISTS unit_name VARCHAR(50);"
        );
        Console.WriteLine("[Startup] stock_ticket_items.notes -> added OK");
        Console.WriteLine("[Startup] stock_ticket_items.unit_name -> added OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Alter stock_ticket_items columns thất bại: {ex.Message}");
    }
}

// Đảm bảo các bảng mới tồn tại: store_approvals, goods_issues, goods_issue_items, goods_issue_attachments.
// EF migration code-first chưa chạy được khi app đang lock bin -> tạo idempotent bằng raw SQL tại startup.
// Tên bảng/cột khớp với Fluent API trong MEPDatabaseContext.OnModelCreating.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MEPDatabaseContext>();
    try
    {
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS store_approvals (
                approval_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                user_id UUID NOT NULL,
                store_name VARCHAR(255) NOT NULL DEFAULT '',
                business_license VARCHAR(255),
                tax_code VARCHAR(100),
                store_address TEXT,
                document_urls TEXT,
                status VARCHAR(50) NOT NULL DEFAULT 'PENDING',
                reject_reason TEXT,
                notes TEXT,
                reviewed_by UUID,
                reviewed_at TIMESTAMP WITHOUT TIME ZONE,
                created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS ix_store_approvals_user ON store_approvals(user_id);
            CREATE INDEX IF NOT EXISTS ix_store_approvals_status ON store_approvals(status);
        ");
        Console.WriteLine("[Startup] store_approvals -> ensured OK");

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS goods_issues (
                goods_issue_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                issue_number VARCHAR(100) NOT NULL DEFAULT '',
                issue_type VARCHAR(100) NOT NULL DEFAULT 'Xuất kho khác',
                customer_id VARCHAR(100),
                customer_name VARCHAR(255),
                description TEXT,
                reference VARCHAR(255),
                issue_date TIMESTAMP WITHOUT TIME ZONE,
                created_by VARCHAR(255),
                created_by_user_id UUID,
                sync_status VARCHAR(50) NOT NULL DEFAULT 'Chưa đồng bộ',
                sync_number VARCHAR(100),
                total_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
                total_payment NUMERIC(18,2),
                payment_method VARCHAR(50),
                branch_id UUID NOT NULL,
                status VARCHAR(50) NOT NULL DEFAULT 'PENDING',
                created_at TIMESTAMP WITHOUT TIME ZONE,
                updated_at TIMESTAMP WITHOUT TIME ZONE,
                CONSTRAINT fk_goods_issues_branch FOREIGN KEY (branch_id) REFERENCES branches(branch_id)
            );
            CREATE INDEX IF NOT EXISTS ix_goods_issues_branch ON goods_issues(branch_id);
            CREATE INDEX IF NOT EXISTS ix_goods_issues_number ON goods_issues(issue_number);
        ");
        Console.WriteLine("[Startup] goods_issues -> ensured OK");

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS goods_issue_items (
                goods_issue_item_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                goods_issue_id UUID NOT NULL,
                line_number INT NOT NULL DEFAULT 1,
                product_id VARCHAR(100),
                product_code VARCHAR(100),
                product_name VARCHAR(500),
                unit VARCHAR(50),
                lot_number VARCHAR(100),
                expiry_date VARCHAR(50),
                warehouse_id VARCHAR(100),
                warehouse_name VARCHAR(255),
                location VARCHAR(255),
                quantity NUMERIC(18,3) NOT NULL DEFAULT 0,
                unit_price NUMERIC(18,2),
                total_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
                CONSTRAINT fk_goods_issue_items_issue FOREIGN KEY (goods_issue_id) REFERENCES goods_issues(goods_issue_id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS ix_goods_issue_items_issue ON goods_issue_items(goods_issue_id);
        ");
        Console.WriteLine("[Startup] goods_issue_items -> ensured OK");

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS goods_issue_attachments (
                goods_issue_attachment_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                goods_issue_id UUID NOT NULL,
                file_name VARCHAR(500) NOT NULL DEFAULT '',
                content_type VARCHAR(255),
                size BIGINT,
                media_url TEXT NOT NULL DEFAULT '',
                uploaded_at TIMESTAMP WITHOUT TIME ZONE,
                uploaded_by UUID,
                CONSTRAINT fk_goods_issue_attachments_issue FOREIGN KEY (goods_issue_id) REFERENCES goods_issues(goods_issue_id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS ix_goods_issue_attachments_issue ON goods_issue_attachments(goods_issue_id);
        ");
        Console.WriteLine("[Startup] goods_issue_attachments -> ensured OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Tạo bảng store_approvals/goods_issues thất bại: {ex.Message}");
    }
}

app.Run();

public class UtcDateTimeConverter : System.Text.Json.Serialization.JsonConverter<DateTime>
{
    public override DateTime Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        return reader.GetDateTime();
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, DateTime value, System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
