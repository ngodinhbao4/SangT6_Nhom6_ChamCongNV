﻿using DoAnCuoiKiNhom6.Data;
using DoAnCuoiKiNhom6.Models;
using DoAnCuoiKiNhom6.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnCuoiKiNhom6.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JwtService _jwtService;

        public AuthController(AppDbContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        // ✅ Đăng ký (Admin hoặc nhân viên)
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User request)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest("Email đã tồn tại");

            var hashed = BCrypt.Net.BCrypt.HashPassword(request.PasswordHash);
            var user = new User
            {
                Email = request.Email,
                PasswordHash = hashed,
                Role = request.Role ?? "EMPLOYEE"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // ✅ Nếu là nhân viên, tự tạo bản ghi trong bảng Employees
            if (user.Role == "EMPLOYEE")
            {
                var employee = new Employee
                {
                    FullName = request.Email.Split('@')[0], // Tạm dùng tên theo email
                    Email = user.Email,
                    HourlyRate = 50000, // Mặc định 50k/h (có thể chỉnh trong DB)
                    IsBanned = false
                };
                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Đăng ký thành công", role = user.Role });
        }

        // ✅ Đăng nhập
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] User request)
        {
            // Tìm user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
                return Unauthorized("Sai email hoặc mật khẩu");

            // Kiểm tra mật khẩu
            if (!BCrypt.Net.BCrypt.Verify(request.PasswordHash, user.PasswordHash))
                return Unauthorized("Sai email hoặc mật khẩu");

            // ✅ Kiểm tra nếu là nhân viên và bị ban thì chặn đăng nhập
            if (user.Role == "EMPLOYEE")
            {
                var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Email == user.Email);
                if (emp != null && emp.IsBanned)
                    return Unauthorized("Tài khoản của bạn đã bị khóa, vui lòng liên hệ quản lý.");
            }

            // ✅ Tạo JWT token
            var token = _jwtService.GenerateToken(user);

            return Ok(new
            {
                message = "Đăng nhập thành công",
                token,
                role = user.Role,
                email = user.Email
            });
        }

        // ✅ Lấy thông tin người dùng hiện tại từ token
        [HttpGet("profile")]
        public IActionResult Profile()
        {
            var email = User?.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            var role = User?.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;

            if (email == null)
                return Unauthorized("Token không hợp lệ hoặc đã hết hạn.");

            return Ok(new
            {
                email,
                role
            });
        }

        // ✅ Kiểm tra token hiện tại (token validity)
        [HttpGet("check-token")]
        [Authorize] // cần gửi token mới vào được
        public IActionResult CheckToken()
        {
            var email = User?.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            var role = User?.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;
            var expClaim = User?.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;

            if (email == null)
                return Unauthorized("Token không hợp lệ hoặc đã hết hạn.");

            // Nếu có claim exp (thời gian hết hạn)
            DateTime? expiry = null;
            if (expClaim != null && long.TryParse(expClaim, out long seconds))
                expiry = DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;

            return Ok(new
            {
                message = "Token hợp lệ",
                email,
                role,
                expiresAt = expiry?.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        // ✅ Đổi mật khẩu
        [HttpPut("change-password")]
        [Authorize] // Cần đăng nhập mới đổi được
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            // Lấy email từ token
            var email = User?.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            if (email == null)
                return Unauthorized("Token không hợp lệ hoặc đã hết hạn.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return NotFound("Không tìm thấy người dùng.");

            // Kiểm tra mật khẩu cũ
            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
                return BadRequest("Mật khẩu cũ không đúng.");

            // Hash mật khẩu mới
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "✅ Đổi mật khẩu thành công!" });
        }

        // ✅ DTO cho yêu cầu đổi mật khẩu
        public class ChangePasswordRequest
        {
            public string OldPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

    }
}
