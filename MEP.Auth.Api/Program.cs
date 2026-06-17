using MEP.Auth.Api.Formatters;
using MEP.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using MEP.Data.Models;
using MEP.Data.Seed;
// Rate Limit
using System.Threading.RateLimiting; 
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Database PostgreSQL
builder.Services.AddDbContext<MEPDatabaseContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Cấu hình Controllers + OData + Content Negotiation + Custom Formatter
builder.Services.AddControllers(options =>
{
    // Kích hoạt Content Negotiation (Tự nhận diện khi Client truyền Header Accept: application/xml)
    options.RespectBrowserAcceptHeader = true;
    options.ReturnHttpNotAcceptable = true;

    // Đăng ký Custom Media Formatter ta vừa tạo ở bước 5
    options.OutputFormatters.Add(new CustomAuthOutputFormatter());
})
.AddXmlSerializerFormatters() // Hỗ trợ định dạng XML nếu có yêu cầu
.AddOData(options => options.Select().Filter().OrderBy().Count().SetMaxTop(100)); // Kích hoạt các bộ lọc của OData

// 3. Cấu hình JWT Authentication
builder.Services.AddScoped<JwtHelper>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});

// 4. Kích hoạt AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// 5. Cấu hình giao diện Swagger hỗ trợ Test truyền mã Token JWT
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventory Service API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Điền chuỗi Token của bạn theo cú pháp: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

//Cấu hình CORS chặt chẽ và chỉ cho phép Frontend (localhost:3000)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // Chỉ định đích danh domain được phép
              .AllowAnyHeader()  
              .AllowAnyMethod()
              .AllowCredentials(); // Refresh Token/Cookie 
    });
});

// Cấu hình Rate Limit
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    // Định nghĩa một policy tên là "StrictLimit"
    options.AddPolicy("StrictLimit", httpContext =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,           // Cho phép tối đa 5 requests
                Window = TimeSpan.FromMinutes(1), // Trong vòng 1 phút
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0             // Vượt mức --> chặn va không đưa vào hàng chờ
            });
    });
});

var app = builder.Build();

// Kích hoạt Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MEPDatabaseContext>();
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db);
}

// Policy cho localhost:3000 và Rate Limit
app.UseCors("AllowReactApp");
app.UseRateLimiter();

// QUAN TRỌNG: Thứ tự kích hoạt Middleware bảo mật (Authentication phải đứng trước Authorization)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();