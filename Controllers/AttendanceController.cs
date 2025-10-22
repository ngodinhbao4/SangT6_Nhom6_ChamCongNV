using DoAnCuoiKiNhom6.Data;
using DoAnCuoiKiNhom6.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnCuoiKiNhom6.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AttendanceController(AppDbContext context)
        {
            _context = context;
        }

        // ✅ Nhân viên chấm công vào bằng thẻ NFC
        [HttpPost("checkin")]
        public async Task<IActionResult> CheckIn([FromBody] string nfcTagId)
        {
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.NfcTagId == nfcTagId);
            if (employee == null)
                return NotFound("Thẻ NFC không hợp lệ!");

            var attendance = new Attendance
            {
                EmployeeId = employee.EmployeeId,
                CheckIn = DateTime.Now
            };

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();
            return Ok(attendance);
        }

        // ✅ Nhân viên chấm công ra
        [HttpPost("checkout/{attendanceId}")]
        public async Task<IActionResult> CheckOut(int attendanceId)
        {
            var attendance = await _context.Attendances.FindAsync(attendanceId);
            if (attendance == null) return NotFound();

            attendance.CheckOut = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(attendance);
        }
    }
}
