using DoAnCuoiKiNhom6.Models;
using Microsoft.EntityFrameworkCore;

namespace DoAnCuoiKiNhom6.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<Payroll> Payrolls { get; set; }
        public DbSet<User> Users { get; set; }

    }
}
