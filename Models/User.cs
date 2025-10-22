using System.ComponentModel.DataAnnotations;

namespace DoAnCuoiKiNhom6.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "EMPLOYEE"; // "ADMIN" hoặc "EMPLOYEE"
    }
}
