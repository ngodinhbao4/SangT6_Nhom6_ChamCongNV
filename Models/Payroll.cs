using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnCuoiKiNhom6.Models
{
    public class Payroll
    {
        [Key]
        public int PayrollId { get; set; }

        [ForeignKey("Employee")]
        public int EmployeeId { get; set; }

        public string Period { get; set; } = string.Empty; // ví dụ "2025-10"
        public decimal TotalHours { get; set; }
        public decimal TotalSalary { get; set; }

        public Employee? Employee { get; set; }
    }
}
