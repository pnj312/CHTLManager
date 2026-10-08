using Microsoft.EntityFrameworkCore;
using CHTLManager.DAL.Entities;

namespace CHTLManager.DAL
{
    public class AppDbContext : DbContext
    {
        // Khai báo kết nối CSDL (Sửa Server Name tương ứng trên máy em)
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=CHTLManagerDB;Trusted_Connection=True;TrustServerCertificate=True;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(user => user.Username).IsRequired().HasMaxLength(450);
                entity.HasIndex(user => user.Username).IsUnique();
                entity.Property(user => user.Password).IsRequired();
                entity.Property(user => user.FullName).IsRequired();
                entity.Property(user => user.Role).IsRequired();

                entity.HasData(
                    new User
                    {
                        Id = 1,
                        Username = "admin",
                        Password = "123456",
                        FullName = "Quản trị viên",
                        Role = "Admin",
                        IsActive = true
                    },
                    new User
                    {
                        Id = 2,
                        Username = "staff",
                        Password = "123456",
                        FullName = "Thu ngân",
                        Role = "Staff",
                        IsActive = true
                    });
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(product => product.Barcode).IsRequired().HasMaxLength(450);
                entity.HasIndex(product => product.Barcode).IsUnique();
                entity.Property(product => product.Name).IsRequired();
                entity.Property(product => product.Unit).IsRequired();
                entity.Property(product => product.ImportPrice).HasColumnType("decimal(18,2)");
                entity.Property(product => product.SellingPrice).HasColumnType("decimal(18,2)");

                entity.HasData(
                    new Product { Id = 1, Barcode = "893000000001", Name = "Coca Cola", ImportPrice = 8000, SellingPrice = 10000, StockQuantity = 50, Unit = "Lon" },
                    new Product { Id = 2, Barcode = "893000000002", Name = "Pepsi", ImportPrice = 8000, SellingPrice = 10000, StockQuantity = 50, Unit = "Lon" },
                    new Product { Id = 3, Barcode = "893000000003", Name = "Nước suối", ImportPrice = 4000, SellingPrice = 6000, StockQuantity = 100, Unit = "Chai" },
                    new Product { Id = 4, Barcode = "893000000004", Name = "Mì Hảo Hảo", ImportPrice = 3000, SellingPrice = 5000, StockQuantity = 100, Unit = "Gói" },
                    new Product { Id = 5, Barcode = "893000000005", Name = "Bánh quy", ImportPrice = 12000, SellingPrice = 15000, StockQuantity = 30, Unit = "Gói" });
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(order => order.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(order => order.CustomerPayment).HasColumnType("decimal(18,2)");
                entity.Property(order => order.ChangeAmount).HasColumnType("decimal(18,2)");
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(order => order.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<OrderDetail>(entity =>
            {
                entity.Property(detail => detail.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(detail => detail.LineTotal).HasColumnType("decimal(18,2)");
                entity.HasOne<Order>()
                    .WithMany()
                    .HasForeignKey(detail => detail.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<Product>()
                    .WithMany()
                    .HasForeignKey(detail => detail.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
    }
}
