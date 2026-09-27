using Microsoft.EntityFrameworkCore;
using CHTLManager.DAL.Entities;

namespace CHTLManager.DAL
{
    public class AppDbContext : DbContext
    {
        // Khai báo kết nối CSDL (Sửa Server Name tương ứng trên máy em)
        protected override OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=.;Database=CHTLManagerDB;Trusted_Connection=True;TrustServerCertificate=True;");
            }
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
    }
}