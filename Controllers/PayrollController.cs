using DoAnCuoiKiNhom6.Data;
using DoAnCuoiKiNhom6.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnCuoiKiNhom6.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "ADMIN")]
    public class PayrollController : ControllerBase
    {
        private readonly AppDbContext _context;
        public PayrollController(AppDbContext context) => _context = context;

        // ✅ Tính lương cho CẢ công ty theo THÁNG (year-month)
        // POST /api/payroll/calculate-month?year=2025&month=10
        [HttpPost("calculate-month")]
        public async Task<IActionResult> CalculateMonth([FromQuery] int year, [FromQuery] int month)
        {
            var (start, end, period) = GetMonthRange(year, month);

            var employees = await _context.Employees.AsNoTracking().ToListAsync();
            var attendances = await _context.Attendances
                .Where(a => a.CheckOut != null && a.CheckIn >= start && a.CheckIn < end)
                .AsNoTracking()
                .ToListAsync();

            var upserts = new List<Payroll>();

            foreach (var emp in employees)
            {
                var hrs = attendances
                    .Where(a => a.EmployeeId == emp.EmployeeId)
                    .Sum(a => (decimal)((a.CheckOut!.Value - a.CheckIn).TotalHours));

                hrs = Math.Round(hrs, 2, MidpointRounding.AwayFromZero);
                var salary = Math.Round(hrs * emp.HourlyRate, 0, MidpointRounding.AwayFromZero);

                var payroll = await _context.Payrolls
                    .FirstOrDefaultAsync(p => p.EmployeeId == emp.EmployeeId && p.Period == period);

                if (payroll == null)
                {
                    payroll = new Payroll
                    {
                        EmployeeId = emp.EmployeeId,
                        Period = period,
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

                upserts.Add(payroll);
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                period,
                employees = upserts.Count,
                items = upserts.Select(p => new
                {
                    p.EmployeeId,
                    p.TotalHours,
                    p.TotalSalary
                }).ToList()
            });
        }

        // ✅ Xem payroll của 1 nhân viên theo THÁNG
        // GET /api/payroll/employee/5?year=2025&month=10
        [HttpGet("employee/{employeeId}")]
        public async Task<IActionResult> GetEmployeePayroll(int employeeId, [FromQuery] int year, [FromQuery] int month)
        {
            var period = $"{year:D4}-{month:D2}";
            var payroll = await _context.Payrolls
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.EmployeeId == employeeId && p.Period == period);

            if (payroll == null) return NotFound("Chưa có payroll cho kỳ này.");

            return Ok(new
            {
                payroll.EmployeeId,
                payroll.Employee!.FullName,
                payroll.Period,
                payroll.TotalHours,
                payroll.TotalSalary
            });
        }

        // ✅ Tính lương theo khoảng ngày (tùy biến) – tuần/quý/năm cũng dùng được
        // POST /api/payroll/calculate-range?start=2025-10-01&end=2025-10-31&label=2025-10
        [HttpPost("calculate-range")]
        public async Task<IActionResult> CalculateRange([FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] string label)
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
            return Ok(new { label, message = "Đã tính lương theo khoảng ngày." });
        }

        private static (DateTime start, DateTime end, string period) GetMonthRange(int year, int month)
        {
            var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);
            var period = $"{year:D4}-{month:D2}";
            return (start, end, period);
        }
    }
}
