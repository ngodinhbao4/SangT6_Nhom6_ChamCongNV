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
    public class PayrollController : ControllerBase
    {
        private readonly AppDbContext _context;
        public PayrollController(AppDbContext context) => _context = context;

        // ================= ADMIN ===================

        // ✅ ADMIN: Tính lương toàn công ty theo khoảng ngày (ngày / tuần / quý / năm)
        [HttpPost("calculate-range")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> CalculateRange(
            [FromQuery] DateTime start,
            [FromQuery] DateTime end,
            [FromQuery] string label)
        {
            if (end <= start) return BadRequest("Khoảng thời gian không hợp lệ.");

            var employees = await _context.Employees.AsNoTracking().ToListAsync();
            var attendances = await _context.Attendances
                .Where(a => a.CheckOut != null && a.CheckIn >= start && a.CheckIn < end)
                .AsNoTracking()
                .ToListAsync();

            foreach (var emp in employees)
            {
                var hrs = attendances
                    .Where(a => a.EmployeeId == emp.EmployeeId)
                    .Sum(a => (decimal)((a.CheckOut!.Value - a.CheckIn).TotalHours));

                hrs = Math.Round(hrs, 2, MidpointRounding.AwayFromZero);
                var salary = Math.Round(hrs * emp.HourlyRate, 0, MidpointRounding.AwayFromZero);

                var payroll = await _context.Payrolls
                    .FirstOrDefaultAsync(p => p.EmployeeId == emp.EmployeeId && p.Period == label);

                if (payroll == null)
                {
                    payroll = new Payroll
                    {
                        EmployeeId = emp.EmployeeId,
                        Period = label,
                        TotalHours = hrs,
                        TotalSalary = salary
                    };
                    _context.Payrolls.Add(payroll);
                }
                else
                {
                    payroll.TotalHours = hrs;
                    payroll.TotalSalary = salary;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { label, message = "✅ Đã tính lương cho toàn công ty." });
        }

        // ✅ ADMIN: Xem tất cả bảng lương (lọc theo kỳ)
        [HttpGet("all")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> GetAllPayrolls([FromQuery] string? period = null)
        {
            var query = _context.Payrolls.Include(p => p.Employee).AsQueryable();

            if (!string.IsNullOrEmpty(period))
                query = query.Where(p => p.Period == period);

            var list = await query
                .OrderByDescending(p => p.Period)
                .Select(p => new
                {
                    p.PayrollId,
                    p.Period,
                    EmployeeName = p.Employee!.FullName,
                    p.TotalHours,
                    p.TotalSalary
                }).ToListAsync();

            return Ok(list);
        }

        // ✅ ADMIN: Xem tổng kết lương (dashboard)
        [HttpGet("summary")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> GetSummary([FromQuery] string period)
        {
            var total = await _context.Payrolls
                .Where(p => p.Period == period)
                .SumAsync(p => p.TotalSalary);

            var avg = await _context.Payrolls
                .Where(p => p.Period == period)
                .AverageAsync(p => p.TotalSalary);

            return Ok(new { period, total, average = Math.Round(avg, 0) });
        }

        // ================= EMPLOYEE ===================

        // ✅ EMPLOYEE: Xem bảng lương của chính mình
        [HttpGet("my")]
        [Authorize(Roles = "EMPLOYEE,ADMIN")]
        public async Task<IActionResult> GetMyPayroll([FromQuery] int year, [FromQuery] int month)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (userEmail == null) return Unauthorized("Không tìm thấy email người dùng.");

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == userEmail);
            if (employee == null) return NotFound("Không tìm thấy thông tin nhân viên.");

            var period = $"{year:D4}-{month:D2}";
            var payroll = await _context.Payrolls
                .FirstOrDefaultAsync(p => p.EmployeeId == employee.EmployeeId && p.Period == period);

            if (payroll == null) return NotFound("Chưa có bảng lương cho kỳ này.");

            return Ok(new
            {
                employee.FullName,
                payroll.Period,
                payroll.TotalHours,
                payroll.TotalSalary
            });
        }

        // ✅ EMPLOYEE: Xem lịch sử lương các tháng trước
        [HttpGet("my-history")]
        [Authorize(Roles = "EMPLOYEE,ADMIN")]
        public async Task<IActionResult> GetMyPayrollHistory()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (userEmail == null) return Unauthorized();

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == userEmail);
            if (employee == null) return NotFound();

            var history = await _context.Payrolls
                .Where(p => p.EmployeeId == employee.EmployeeId)
                .OrderByDescending(p => p.Period)
                .Select(p => new
                {
                    p.Period,
                    p.TotalHours,
                    p.TotalSalary
                })
                .ToListAsync();

            return Ok(history);
        }
    }
}
