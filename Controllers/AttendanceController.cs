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

        // ✅ Quét thẻ NFC – tự động check-in hoặc check-out
        [HttpPost("tap")]
        public async Task<IActionResult> Tap([FromBody] string nfcTagId)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.NfcTagId == nfcTagId);

            if (employee == null)
                return NotFound("❌ Thẻ NFC không hợp lệ!");

            var now = DateTime.Now;
            var today = now.Date;

            // Tìm bản ghi chưa check-out của nhân viên
            var openAttendance = await _context.Attendances
                .Where(a => a.EmployeeId == employee.EmployeeId && a.CheckOut == null)
                .OrderByDescending(a => a.CheckIn)
                .FirstOrDefaultAsync();

            // Nếu có bản ghi hôm nay → check-out
            if (openAttendance != null && openAttendance.CheckIn.Date == today)
            {
                openAttendance.CheckOut = now;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    action = "checkout",
                    message = $"✅ {employee.FullName} đã check-out lúc {now:HH:mm:ss}",
                    attendanceId = openAttendance.AttendanceId,
                    checkIn = openAttendance.CheckIn,
                    checkOut = openAttendance.CheckOut
                });
            }

            // Nếu chưa có bản ghi hôm nay → check-in
            var attendance = new Attendance
            {
                EmployeeId = employee.EmployeeId,
                CheckIn = now
            };

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                action = "checkin",
                message = $"✅ {employee.FullName} đã check-in lúc {now:HH:mm:ss}",
                attendanceId = attendance.AttendanceId,
                checkIn = attendance.CheckIn
            });
        }
    }
}
