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

// ✅ 2. Cấu hình dịch vụ
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

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
        Description = "Nhập token theo định dạng: **Bearer {token}**"
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

// ✅ 4. Cấu hình JWT Authentication
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
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])
            )
        };
    });

// ✅ 5. Đăng ký các service
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

// ✅ 6. Bật Swagger luôn khi chạy
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Attendance API v1");
    options.RoutePrefix = string.Empty;
});

// ✅ 7. Bảo mật và routing
app.UseHttpsRedirection();
app.UseAuthentication();  // 🔥 Bắt buộc đặt trước Authorization
app.UseAuthorization();
app.MapControllers();

// ✅ 8. Tự động mở Swagger trên trình duyệt
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        var url = app.Urls.FirstOrDefault() ?? "https://localhost:7161";
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
        Console.WriteLine($"✅ Swagger đã mở tại: {url}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Không thể mở Swagger: {ex.Message}");
    }
});

// ✅ 9. Tự động tạo Admin mặc định
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

        Console.WriteLine("✅ Tài khoản ADMIN mặc định đã được tạo:");
        Console.WriteLine("   Email: admin@company.com");
        Console.WriteLine("   Password: admin123");
    }
    else
    {
        Console.WriteLine("✅ Đã có ít nhất 1 tài khoản ADMIN, bỏ qua tạo mặc định.");
    }
}

// 🚀 10. Chạy ứng dụng
app.Run();
