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

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Thêm nhân viên thành công",
                employee
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
    }
}
