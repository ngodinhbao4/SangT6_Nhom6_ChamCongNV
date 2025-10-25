using DoAnCuoiKiNhom6.Data;
using DoAnCuoiKiNhom6.Models;
using DoAnCuoiKiNhom6.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Diagnostics;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ✅ 1. Kết nối MySQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 36))));

// ✅ 2. Dịch vụ MVC & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// (Tuỳ chọn) CORS mở để test nhanh (có thể siết lại sau)
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("AllowAll", p =>
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// ✅ 3. Swagger + JWT authorize
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Attendance System API",
        Version = "v1",
        Description = "API chấm công nhân viên sử dụng thẻ NFC, vân tay hoặc khuôn mặt."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập token: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] {}
        }
    });
});

// ✅ 4. JWT Auth
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// ✅ 5. Đăng ký service
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

// ✅ 6. Swagger
app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "Attendance API v1");
    o.RoutePrefix = string.Empty; // mở swagger ở "/"
});

// ✅ 7. Pipeline
// ❌ BỎ UseHttpsRedirection khi bạn chỉ host HTTP 7161 (tránh bị redirect sang https)
// app.UseHttpsRedirection();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ✅ 8. Tự mở Swagger (ép 0.0.0.0 -> localhost để trình duyệt mở được)
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        var first = app.Urls.FirstOrDefault() ?? "http://localhost:7161";
        var openUrl = first.Replace("0.0.0.0", "localhost");
        Process.Start(new ProcessStartInfo { FileName = openUrl, UseShellExecute = true });
        Console.WriteLine($"✅ Swagger đã mở tại: {openUrl}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Không thể mở Swagger: {ex.Message}");
    }
});

// ✅ 9. Tạo Admin mặc định (nếu chưa có)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!db.Users.Any(u => u.Role == "ADMIN"))
    {
        var defaultAdmin = new User
        {
            Email = "admin@company.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = "ADMIN"
        };
        db.Users.Add(defaultAdmin);
        db.SaveChanges();
        Console.WriteLine("✅ Đã tạo ADMIN mặc định: admin@company.com / admin123");
    }
    else
    {
        Console.WriteLine("✅ Đã có ít nhất 1 tài khoản ADMIN, bỏ qua tạo mặc định.");
    }
}

// 🚀 10. Chạy ứng dụng: lắng nghe mọi IP trên cổng 7161 (cho thiết bị LAN truy cập)
app.Run("http://0.0.0.0:7161");
