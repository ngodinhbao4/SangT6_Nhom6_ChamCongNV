using DoAnCuoiKiNhom6.Data;
using DoAnCuoiKiNhom6.Models;
using DoAnCuoiKiNhom6.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Kết nối MySQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
    new MySqlServerVersion(new Version(8, 0, 36))));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

builder.Services.AddScoped<JwtService>();

var app = builder.Build();

// Luôn bật Swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Attendance API v1");
    options.RoutePrefix = string.Empty; // Mở swagger tại URL gốc
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.UseAuthentication();
app.UseAuthorization();

// ✅ Đặt lệnh mở trình duyệt NGAY SAU khi server khởi động
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        var url = app.Urls.FirstOrDefault() ?? "https://localhost:7161"; // đổi port nếu khác
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true // mở bằng trình duyệt mặc định
        });
        Console.WriteLine($"Swagger đã mở tại: {url}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Không thể mở Swagger: {ex.Message}");
    }
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // ✅ Tự động tạo tài khoản ADMIN mặc định nếu chưa có
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

        Console.WriteLine("✅ Tài khoản ADMIN mặc định đã được tạo:");
        Console.WriteLine("   Email: admin@company.com");
        Console.WriteLine("   Password: admin123");
    }
    else
    {
        Console.WriteLine("✅ Đã có ít nhất 1 tài khoản ADMIN, bỏ qua tạo mặc định.");
    }
}

// 🚀 Chạy ứng dụng
app.Run();
