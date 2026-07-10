using MEP_Management_System.Authorization;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Services.InventoryService;
using MEP_Management_System.Hubs;
using MEP_Management_System.DTOs.SaleDTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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
builder.Services.AddScoped<INotificationSender, LogNotificationSender>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentBranchAccessor, CurrentBranchAccessor>();
builder.Services.AddScoped<IOrderDiscountService, OrderDiscountService>();
builder.Services.AddScoped<IInwardInventoryExcelService, InwardInventoryExcelService>();

// OData
var odataBuilder = new ODataConventionModelBuilder();
odataBuilder.EntitySet<User>("Users");
odataBuilder.EntitySet<Role>("Roles");
var postsCfg = odataBuilder.EntitySet<CommunityPost>("CommunityPosts");
postsCfg.EntityType.HasKey(p => p.PostId);
var catsCfg = odataBuilder.EntitySet<CommunityCategory>("CommunityCategories");
catsCfg.EntityType.HasKey(c => c.CategoryId);
var reportsCfg = odataBuilder.EntitySet<ViolationReport>("ViolationReports");
reportsCfg.EntityType.HasKey(r => r.ReportId);
var logsCfg = odataBuilder.EntitySet<SystemLog>("SystemLogs");
logsCfg.EntityType.HasKey(l => l.LogId);
var notifCfg = odataBuilder.EntitySet<SystemNotification>("SystemNotifications");
notifCfg.EntityType.HasKey(n => n.NotificationId);
var approvalsCfg = odataBuilder.EntitySet<StoreApproval>("StoreApprovals");
approvalsCfg.EntityType.HasKey(a => a.ApprovalId);

// Register VietQR Service
builder.Services.Configure<VietQrOptions>(builder.Configuration.GetSection("VietQr"));
builder.Services.AddHttpClient<VietQrService>();
builder.Services.AddControllers()
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
        policy.WithOrigins("http://localhost:3000")
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

app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MEP API V1");

    c.HeadContent = @"
        <script>
            window.addEventListener('load', function() {
                setTimeout(function() {
                    
                    var token = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjIwMDAwMDAwLTAwMDAtMDAwMC0wMDAwLTAwMDAwMDAwMDAwMiIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL25hbWUiOiJvd25lci5iYWNAbWVwLnZuIiwiaHR0cDovL3NjaGVtYXMueG1sc29hcC5vcmcvd3MvMjAwNS8wNS9pZGVudGl0eS9jbGFpbXMvZ2l2ZW5uYW1lIjoiVHLhuqduIFbEg24gQuG6r2MgKE93bmVyKSIsImJyYW5jaElkIjoiMzAwMDAwMDAtMDAwMC0wMDAwLTAwMDAtMDAwMDAwMDAwMDAxIiwiaHR0cDovL3NjaGVtYXMubWljcm9zb2Z0LmNvbS93cy8yMDA4LzA2L2lkZW50aXR5L2NsYWltcy9yb2xlIjoiT3duZXIiLCJleHAiOjE4MTUxOTA1ODQsImlzcyI6IkludmVudG9yeVNlcnZpY2VBUEkiLCJhdWQiOiJJbnZlbnRvcnlTZXJ2aWNlVXNlcnMifQ.dHgDabbWdbakPGjzKvtQboFx7a1z282IPW5fNAmDpbU';
                        console.log('--- Swagger đã tự động Đăng nhập tài khoản Owner thành công! ---');
                    }
                }, 10000);
            });
        </script>
    ";
});

app.UseCors("AllowFrontend");
// app.UseHttpsRedirection(); // Bỏ comment khi deploy lên server thật có SSL/TLS
app.UseAuthentication();
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
