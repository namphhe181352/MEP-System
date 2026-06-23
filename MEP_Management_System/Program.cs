using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Services.InventoryService;
using MEP_Management_System.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OData.ModelBuilder;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MEPDatabaseContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// Services
builder.Services.AddSingleton<JwtHelper>();
builder.Services.AddScoped<INotificationSender, LogNotificationSender>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentBranchAccessor, CurrentBranchAccessor>();

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
builder.Services.AddControllers().AddOData(options =>
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
});

builder.Services.AddScoped<IInventoryReportService, InventoryReportService>();
builder.Services.AddSignalR();
builder.Services.AddAuthorization();

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
                    
                    var token = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6Ijk4NmM0NWQwLWFhMjctNDdkMi1iNzdmLWFiN2ZkZTc4ZTZiOCIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL25hbWUiOiIxMjNAZ21haWwuY29tIiwiaHR0cDovL3NjaGVtYXMueG1sc29hcC5vcmcvd3MvMjAwNS8wNS9pZGVudGl0eS9jbGFpbXMvZ2l2ZW5uYW1lIjoiMTIzNDU2IiwiaHR0cDovL3NjaGVtYXMubWljcm9zb2Z0LmNvbS93cy8yMDA4LzA2L2lkZW50aXR5L2NsYWltcy9yb2xlIjoiT3duZXIiLCJleHAiOjE4MTM3MzgxNzQsImlzcyI6IkludmVudG9yeVNlcnZpY2VBUEkiLCJhdWQiOiJJbnZlbnRvcnlTZXJ2aWNlVXNlcnMifQ.0UatgtjG4s4lVXG7s4_74ujWWXbQZDw7ZlwsCY8LG6g';
                    if (token) {
                        ui.preauthorizeApiKey('Bearer', token);
                        console.log('--- Swagger đã tự động Đăng nhập tài khoản Owner thành công! ---');
                    }
                }, 1000);
            });
        </script>
    ";
});

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<InventoryHub>("/r/inventoryHub");

app.Run();
