using DoAnCuoiKiNhom6.Data;
using DoAnCuoiKiNhom6.Models;
using DoAnCuoiKiNhom6.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DoAnCuoiKiNhom6.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "ADMIN")]
    public class ReportController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _email;

        public ReportController(AppDbContext context, IEmailService email)
        {
            _context = context;
            _email = email;
        }

        // ✅ Tóm tắt toàn công ty theo THÁNG
        // GET /api/report/summary-month?year=2025&month=10
        [HttpGet("summary-month")]
        public async Task<IActionResult> SummaryMonth([FromQuery] int year, [FromQuery] int month)
        {
            var period = $"{year:D4}-{month:D2}";

            var items = await _context.Payrolls
                .Include(p => p.Employee)
                .Where(p => p.Period == period)
                .Select(p => new
                {
                    p.EmployeeId,
                    p.Employee!.FullName,
                    p.Employee.Email,
                    p.TotalHours,
                    p.TotalSalary
                })
                .ToListAsync();

            var totalHours = items.Sum(i => i.TotalHours);
            var totalSalary = items.Sum(i => i.TotalSalary);

            return Ok(new
            {
                period,
                totalEmployees = items.Count,
                totalHours,
                totalSalary,
                items
            });
        }

        // ✅ Gửi email báo cáo tháng cho TỪNG nhân viên (dựa trên Payroll đã tính)
        // POST /api/report/send-monthly?year=2025&month=10
        [HttpPost("send-monthly")]
        public async Task<IActionResult> SendMonthly([FromQuery] int year, [FromQuery] int month)
        {
            var period = $"{year:D4}-{month:D2}";
            var payrolls = await _context.Payrolls
                .Include(p => p.Employee)
                .Where(p => p.Period == period)
                .ToListAsync();

            if (payrolls.Count == 0)
                return BadRequest("Chưa có payroll cho kỳ này. Hãy chạy /api/payroll/calculate-month trước.");

            int success = 0, fail = 0;

            foreach (var p in payrolls)
            {
                if (string.IsNullOrWhiteSpace(p.Employee?.Email))
                {
                    fail++; continue;
                }

                var body = BuildEmployeeReportHtml(p.Employee!.FullName, period, p.TotalHours, p.TotalSalary);
                try
                {
                    await _email.SendAsync(p.Employee!.Email, $"Báo cáo công việc {period}", body);
                    success++;
                }
                catch
                {
                    fail++;
                }
            }

            return Ok(new
            {
                period,
                sent = success,
                failed = fail,
                message = "Đã xử lý gửi mail báo cáo theo tháng."
            });
        }

        // ✅ Xem báo cáo chi tiết 1 nhân viên theo THÁNG (không gửi mail)
        // GET /api/report/employee-month/5?year=2025&month=10
        [HttpGet("employee-month/{employeeId}")]
        public async Task<IActionResult> EmployeeMonth(int employeeId, [FromQuery] int year, [FromQuery] int month)
        {
            var period = $"{year:D4}-{month:D2}";
            var payroll = await _context.Payrolls
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.EmployeeId == employeeId && p.Period == period);

            if (payroll == null) return NotFound("Chưa có payroll cho kỳ này.");

            var detail = await _context.Attendances
                .Where(a => a.EmployeeId == employeeId && a.CheckIn >= new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc) && a.CheckIn < new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1))
                .OrderBy(a => a.CheckIn)
                .ToListAsync();

            return Ok(new
            {
                payroll.Employee!.FullName,
                payroll.Employee.Email,
                payroll.Period,
                payroll.TotalHours,
                payroll.TotalSalary,
                attendanceDetails = detail.Select(d => new
                {
                    d.AttendanceId,
                    d.CheckIn,
                    d.CheckOut,
                    Hours = d.CheckOut == null ? 0 : Math.Round((decimal)(d.CheckOut.Value - d.CheckIn).TotalHours, 2)
                })
            });
        }

        private static string BuildEmployeeReportHtml(string fullName, string period, decimal totalHours, decimal totalSalary)
        {
            var sb = new StringBuilder();
            sb.Append($@"
                <div style='font-family:Arial,sans-serif'>
                    <h2>Báo cáo chấm công – {period}</h2>
                    <p>Xin chào <b>{fullName}</b>,</p>
                    <p>Dưới đây là tổng hợp công việc trong kỳ <b>{period}</b>:</p>
                    <ul>
                        <li><b>Tổng giờ làm:</b> {totalHours} giờ</li>
                        <li><b>Tổng lương ước tính:</b> {totalSalary:N0} VND</li>
                    </ul>
                    <p>Nếu có sai sót, vui lòng liên hệ quản lý để điều chỉnh.</p>
                    <hr/>
                    <p>Trân trọng,<br/>Attendance System</p>
                </div>
            ");
            return sb.ToString();
        }
    }
}
