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

        // ===================== Helpers dùng chung =====================

        public enum PeriodKind { day, week, month, quarter, year }

        private static DateTime StartOfWeekMonday(DateTime dUtc)
        {
            // dUtc là UTC DateTime.Date
            var dow = (int)dUtc.DayOfWeek; // 0=Sun..6=Sat
            // Tuần bắt đầu Thứ 2
            // Nếu Chủ nhật => lùi 6 ngày; các ngày khác => lùi (weekday-1)
            int back = dow == 0 ? 6 : (dow - 1);
            return dUtc.AddDays(-back);
        }

        private static (DateTime startUtc, DateTime endUtc, string label) MakeRange(PeriodKind kind, DateTime anchorUtc)
        {
            anchorUtc = anchorUtc.Date; // chốt 00:00 UTC
            switch (kind)
            {
                case PeriodKind.day:
                    {
                        var s = anchorUtc;
                        var e = s.AddDays(1);
                        var label = s.ToString("yyyy-MM-dd");
                        return (s, e, label);
                    }
                case PeriodKind.week:
                    {
                        var s = StartOfWeekMonday(anchorUtc);
                        var e = s.AddDays(7);
                        // tuần tương đối trong năm (đơn giản, nhất quán với frontend)
                        var first = new DateTime(anchorUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        var weekNo = (int)Math.Ceiling((anchorUtc - first).TotalDays / 7.0 + (int)first.DayOfWeek / 7.0);
                        if (weekNo < 1) weekNo = 1;
                        var label = $"{anchorUtc:yyyy}-W{weekNo:00}";
                        return (s, e, label);
                    }
                case PeriodKind.month:
                    {
                        var s = new DateTime(anchorUtc.Year, anchorUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                        var e = s.AddMonths(1);
                        var label = $"{anchorUtc:yyyy}-{anchorUtc.Month:00}";
                        return (s, e, label);
                    }
                case PeriodKind.quarter:
                    {
                        var q = ((anchorUtc.Month - 1) / 3) + 1; // 1..4
                        var startMonth = (q - 1) * 3 + 1;        // 1,4,7,10
                        var s = new DateTime(anchorUtc.Year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
                        var e = s.AddMonths(3);
                        var label = $"{anchorUtc:yyyy}-Q{q}";
                        return (s, e, label);
                    }
                default: // year
                    {
                        var s = new DateTime(anchorUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        var e = s.AddYears(1);
                        var label = $"{anchorUtc:yyyy}";
                        return (s, e, label);
                    }
            }
        }

        private sealed record ReportItem(int EmployeeId, string FullName, string? Email, decimal Hours, decimal Salary);
        private sealed record ReportSummary(
            string Period, DateTime StartUtc, DateTime EndUtc,
            int EmployeeCount, decimal TotalHours, decimal TotalSalary,
            List<ReportItem> Items
        );

        private async Task<ReportSummary> BuildSummaryAsync(DateTime startUtc, DateTime endUtc, string label)
        {
            // Lấy attendances trong khoảng UTC
            var attends = await _context.Attendances
                .Where(a => a.CheckOut != null && a.CheckIn >= startUtc && a.CheckIn < endUtc)
                .AsNoTracking()
                .ToListAsync();

            var employees = await _context.Employees.AsNoTracking().ToListAsync();

            var items = attends
                .GroupBy(a => a.EmployeeId)
                .Select(g =>
                {
                    var emp = employees.FirstOrDefault(x => x.EmployeeId == g.Key);
                    var hrs = (decimal)g.Sum(x => (x.CheckOut!.Value - x.CheckIn).TotalHours);
                    hrs = Math.Round(hrs, 2, MidpointRounding.AwayFromZero);
                    var salary = Math.Round(hrs * (emp?.HourlyRate ?? 0m), 0, MidpointRounding.AwayFromZero);
                    return new ReportItem(
                        EmployeeId: g.Key,
                        FullName: emp?.FullName ?? $"Emp#{g.Key}",
                        Email: emp?.Email,
                        Hours: hrs,
                        Salary: salary
                    );
                })
                .OrderByDescending(i => i.Salary)
                .ToList();

            return new ReportSummary(
                Period: label,
                StartUtc: startUtc,
                EndUtc: endUtc,
                EmployeeCount: items.Count,
                TotalHours: items.Sum(i => i.Hours),
                TotalSalary: items.Sum(i => i.Salary),
                Items: items
            );
        }

        private static byte[] BuildCsvBytes(ReportSummary s)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Period,{s.Period}");
            sb.AppendLine($"Start,{s.StartUtc:yyyy-MM-dd}");
            sb.AppendLine($"End,{s.EndUtc:yyyy-MM-dd}");
            sb.AppendLine($"EmployeeCount,{s.EmployeeCount}");
            sb.AppendLine($"TotalHours,{s.TotalHours}");
            sb.AppendLine($"TotalSalary,{s.TotalSalary}");
            sb.AppendLine();
            sb.AppendLine("EmployeeId,FullName,Email,Hours,Salary");
            foreach (var i in s.Items)
            {
                var name = (i.FullName ?? "").Replace("\"", "\"\"");
                var email = (i.Email ?? "").Replace("\"", "\"\"");
                sb.AppendLine($"{i.EmployeeId},\"{name}\",\"{email}\",{i.Hours},{i.Salary}");
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private async Task<IActionResult> SummaryInternalAsync(PeriodKind kind, DateTime? anchorUtc)
        {
            var anchor = (anchorUtc ?? DateTime.UtcNow).Date;
            var (s, e, label) = MakeRange(kind, anchor);
            var summary = await BuildSummaryAsync(s, e, label);
            return Ok(summary);
        }

        private async Task<IActionResult> SummaryRangeInternalAsync(DateTime startUtc, DateTime endUtc, string? label = null)
        {
            if (endUtc <= startUtc) return BadRequest("Khoảng thời gian không hợp lệ.");
            var summary = await BuildSummaryAsync(startUtc.Date, endUtc.Date, label ?? $"{startUtc:yyyy-MM-dd}..{endUtc:yyyy-MM-dd}");
            return Ok(summary);
        }

        private async Task<IActionResult> SendEmailsFromSummaryAsync(ReportSummary s, string subjectPrefix)
        {
            int success = 0, fail = 0;
            foreach (var i in s.Items)
            {
                if (string.IsNullOrWhiteSpace(i.Email)) { fail++; continue; }
                var body = BuildEmployeeReportHtml(i.FullName, s.Period, i.Hours, i.Salary);
                try
                {
                    await _email.SendAsync(i.Email!, $"{subjectPrefix} {s.Period}", body);
                    success++;
                }
                catch { fail++; }
            }
            return Ok(new { period = s.Period, sent = success, failed = fail, message = "Đã gửi email báo cáo." });
        }

        // ===================== 1) SUMMARY THEO KỲ (Tuần/Tháng/Quý/Năm/Ngày) =====================

        // GET /api/report/summary?kind=month&anchor=2025-10-01 (anchor UTC, optional)
        [HttpGet("summary")]
        public Task<IActionResult> Summary([FromQuery] PeriodKind kind = PeriodKind.month, [FromQuery] DateTime? anchor = null)
            => SummaryInternalAsync(kind, anchor);

        // GET /api/report/summary-range?start=2025-10-01&end=2025-10-31&label=T10-2025
        [HttpGet("summary-range")]
        public Task<IActionResult> SummaryRange([FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] string? label = null)
            => SummaryRangeInternalAsync(start, end, label);

        // GET /api/report/summary.csv?kind=quarter&anchor=2025-10-01
        [HttpGet("summary.csv")]
        public async Task<IActionResult> SummaryCsv([FromQuery] PeriodKind kind = PeriodKind.month, [FromQuery] DateTime? anchor = null)
        {
            var anchorUtc = (anchor ?? DateTime.UtcNow).Date;
            var (s, e, label) = MakeRange(kind, anchorUtc);
            var summary = await BuildSummaryAsync(s, e, label);
            var bytes = BuildCsvBytes(summary);
            return File(bytes, "text/csv; charset=utf-8", $"report_{summary.Period}.csv");
        }

        // GET /api/report/summary-range.csv?start=2025-10-01&end=2025-10-31&label=Oct-2025
        [HttpGet("summary-range.csv")]
        public async Task<IActionResult> SummaryRangeCsv([FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] string? label = null)
        {
            if (end <= start) return BadRequest("Khoảng thời gian không hợp lệ.");
            var summary = await BuildSummaryAsync(start.Date, end.Date, label ?? $"{start:yyyy-MM-dd}..{end:yyyy-MM-dd}");
            var bytes = BuildCsvBytes(summary);
            return File(bytes, "text/csv; charset=utf-8", $"report_{summary.Period}.csv");
        }

        // ===================== 2) GỬI EMAIL THEO KỲ / THEO KHOẢNG =====================

        // POST /api/report/send?kind=week&anchor=2025-10-21
        [HttpPost("send")]
        public async Task<IActionResult> Send([FromQuery] PeriodKind kind = PeriodKind.month, [FromQuery] DateTime? anchor = null)
        {
            var anchorUtc = (anchor ?? DateTime.UtcNow).Date;
            var (s, e, label) = MakeRange(kind, anchorUtc);
            var summary = await BuildSummaryAsync(s, e, label);
            return await SendEmailsFromSummaryAsync(summary, "Báo cáo công việc");
        }

        // POST /api/report/send-range?start=2025-10-01&end=2025-10-31&label=T10-2025
        [HttpPost("send-range")]
        public async Task<IActionResult> SendRange([FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] string? label = null)
        {
            if (end <= start) return BadRequest("Khoảng thời gian không hợp lệ.");
            var summary = await BuildSummaryAsync(start.Date, end.Date, label ?? $"{start:yyyy-MM-dd}..{end:yyyy-MM-dd}");
            return await SendEmailsFromSummaryAsync(summary, "Báo cáo công việc");
        }

        // ===================== 3) API THÁNG DÙNG PAYROLLS (GIỮ NGUYÊN API CŨ CỦA BẠN) =====================

        // ✅ Tóm tắt toàn công ty theo THÁNG từ bảng Payrolls
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

        // ✅ Xem báo cáo chi tiết 1 nhân viên theo THÁNG (dùng Payrolls + liệt kê Attendance)
        // GET /api/report/employee-month/5?year=2025&month=10
        [HttpGet("employee-month/{employeeId}")]
        public async Task<IActionResult> EmployeeMonth(int employeeId, [FromQuery] int year, [FromQuery] int month)
        {
            var period = $"{year:D4}-{month:D2}";
            var payroll = await _context.Payrolls
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.EmployeeId == employeeId && p.Period == period);

            if (payroll == null) return NotFound("Chưa có payroll cho kỳ này.");

            var monthStartUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var detail = await _context.Attendances
                .Where(a => a.EmployeeId == employeeId && a.CheckIn >= monthStartUtc && a.CheckIn < monthStartUtc.AddMonths(1))
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

        // ===================== Template Email =====================
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

        // ✅ GỬI EMAIL THÔNG BÁO LƯƠNG (chỉ gửi nội dung, không đính kèm file)
        // POST /api/report/send-summary?year=2025&month=10
        [HttpPost("send-summary")]
        public async Task<IActionResult> SendSalarySummary(
            [FromQuery] int year,
            [FromQuery] int month)
        {
            var period = $"{year:D4}-{month:D2}";

            var payrolls = await _context.Payrolls
                .Include(p => p.Employee)
                .Where(p => p.Period == period)
                .ToListAsync();

            if (payrolls.Count == 0)
                return BadRequest("⚠️ Chưa có bảng lương cho kỳ này. Hãy tính lương trước.");

            int success = 0, fail = 0;

            foreach (var p in payrolls)
            {
                if (string.IsNullOrWhiteSpace(p.Employee?.Email))
                {
                    fail++;
                    continue;
                }

                // ✅ Soạn nội dung HTML thân thiện
                var body = $@"
        <div style='font-family:Arial,sans-serif; line-height:1.5'>
            <h2 style='color:#2b6cb0;'>Báo cáo lương tháng {month:D2}/{year}</h2>
            <p>Xin chào <b>{p.Employee!.FullName}</b>,</p>
            <p>Dưới đây là kết quả chấm công và lương của bạn trong kỳ <b>{period}</b>:</p>
            <ul>
                <li><b>Tổng giờ làm:</b> {p.TotalHours:N2} giờ</li>
                <li><b>Tổng lương:</b> {p.TotalSalary:N0} ₫</li>
            </ul>
            <p>Nếu có sai sót, vui lòng liên hệ quản lý để được kiểm tra lại.</p>
            <p style='margin-top:12px'>Trân trọng,<br/>Attendance System</p>
            <hr/>
            <small style='color:gray'>Email này được gửi tự động từ hệ thống chấm công.</small>
        </div>";

                try
                {
                    await _email.SendAsync(
                        p.Employee!.Email,
                        $"[Báo cáo lương] {p.Employee.FullName} – {period}",
                        body
                    );
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
                message = $"Đã gửi báo cáo lương qua email cho {success} nhân viên, thất bại {fail}."
            });
        }

        [HttpPost("send-test")]
        public async Task<IActionResult> SendTest([FromQuery] string to)
        {
            try
            {
                await _email.SendAsync(to, "Test email", "<b>Đây là email test từ hệ thống Attendance</b>");
                return Ok(new { ok = true, to });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { ok = false, error = ex.Message });
            }
        }

    }
}
