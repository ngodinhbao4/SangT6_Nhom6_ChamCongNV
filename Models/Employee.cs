using System.ComponentModel.DataAnnotations;

namespace DoAnCuoiKiNhom6.Models
{
    public class Employee
    {
        [Key]
        public int EmployeeId { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? NfcTagId { get; set; } // Mã NFC đăng ký

        public decimal HourlyRate { get; set; } // Lương/giờ

        public bool IsBanned { get; set; } = false;
    }
}
