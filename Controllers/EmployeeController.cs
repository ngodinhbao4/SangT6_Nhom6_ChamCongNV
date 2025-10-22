using DoAnCuoiKiNhom6.Data;
using DoAnCuoiKiNhom6.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DoAnCuoiKiNhom6.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // ✅ Bắt buộc phải đăng nhập (JWT)
    public class EmployeeController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EmployeeController(AppDbContext context)
        {
            _context = context;
        }

        // ✅ Chỉ ADMIN mới được xem toàn bộ danh sách nhân viên
        [HttpGet]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> GetAll()
        {
            var employees = await _context.Employees.ToListAsync();
            return Ok(employees);
        }

        // ✅ Chỉ ADMIN mới được thêm nhân viên
        [HttpPost]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Create(Employee employee)
        {
            if (string.IsNullOrEmpty(employee.FullName) || string.IsNullOrEmpty(employee.Email))
                return BadRequest("Thiếu thông tin nhân viên.");

            // ⚠️ Kiểm tra trùng email trong bảng Users
            if (await _context.Users.AnyAsync(u => u.Email == employee.Email))
                return BadRequest("Email này đã tồn tại trong hệ thống.");

            // ✅ 1. Thêm nhân viên vào bảng Employees
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            // ✅ 2. Tạo tài khoản User tương ứng
            var defaultPassword = "123456";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(defaultPassword);

            var user = new User
            {
                Email = employee.Email,
                PasswordHash = hashedPassword,
                Role = "EMPLOYEE"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Thêm nhân viên và tạo tài khoản thành công",
                employee = new
                {
                    employee.EmployeeId,
                    employee.FullName,
                    employee.Email,
                    employee.HourlyRate,
                    employee.NfcTagId
                },
                defaultLogin = new
                {
                    email = employee.Email,
                    password = defaultPassword
                }
            });
        }

        // ✅ Nhân viên xem thông tin cá nhân (dựa theo email trong token JWT)
        [HttpGet("me")]
        [Authorize(Roles = "EMPLOYEE,ADMIN")]
        public async Task<IActionResult> GetMyProfile()
        {
            var email = User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            if (email == null)
                return Unauthorized("Không tìm thấy thông tin người dùng trong token.");

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == email);

            if (employee == null)
                return NotFound("Không tìm thấy nhân viên.");

            return Ok(employee);
        }

        // ✅ Admin có thể ban / gỡ ban một nhân viên
        [HttpPut("ban/{id}")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> BanEmployee(int id, [FromQuery] bool isBanned = true)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
                return NotFound("Không tìm thấy nhân viên.");

            employee.IsBanned = isBanned;
            await _context.SaveChangesAsync();

            string status = isBanned ? "đã bị khóa" : "đã được mở khóa";
            return Ok(new { message = $"Nhân viên {employee.FullName} {status}." });
        }

        // ✅ Quét NFC để thêm/cập nhật UID cho nhân viên
        [HttpPut("scan-nfc")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> ScanAndAssignNfc([FromBody] AssignNfcRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NfcTagId))
                return BadRequest("Vui lòng cung cấp UID của thẻ NFC.");

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == request.EmployeeId);
            if (employee == null)
                return NotFound("Không tìm thấy nhân viên.");

            // ⚠️ Kiểm tra nếu UID đã được dùng cho nhân viên khác
            var duplicate = await _context.Employees
                .FirstOrDefaultAsync(e => e.NfcTagId == request.NfcTagId && e.EmployeeId != request.EmployeeId);
            if (duplicate != null)
                return BadRequest($"Thẻ này đã được gán cho nhân viên khác: {duplicate.FullName}");

            // ✅ Cập nhật UID NFC cho nhân viên
            employee.NfcTagId = request.NfcTagId;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Thêm hoặc cập nhật UID NFC thành công!",
                employeeId = employee.EmployeeId,
                fullName = employee.FullName,
                nfcTagId = employee.NfcTagId
            });
        }
    }

    // ✅ DTO cho request quét NFC
    public class AssignNfcRequest
    {
        public int EmployeeId { get; set; }
        public string NfcTagId { get; set; } = string.Empty;
    }
}
