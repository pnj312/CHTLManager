using CHTLManager.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CHTLManager.DAL;

/// <summary>Kết nối CSDL SQLite và cấu hình ánh xạ các Entity.</summary>
public class AppDbContext : DbContext
{
    /// <summary>Đường dẫn file CSDL. Mặc định: %LocalAppData%\CHTLManager\chtlmanager.db</summary>
    public static string DbPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CHTLManager", "chtlmanager.db");

    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
            optionsBuilder.UseSqlite($"Data Source={DbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(p => p.Barcode).IsUnique();
            e.Property(p => p.Price).HasColumnType("decimal(18,0)");
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasIndex(o => o.Code).IsUnique();
            e.Property(o => o.Total).HasColumnType("decimal(18,0)");
            e.Property(o => o.CustomerPaid).HasColumnType("decimal(18,0)");
            e.Property(o => o.Change).HasColumnType("decimal(18,0)");
            e.HasOne(o => o.Cashier)
             .WithMany(u => u.Orders)
             .HasForeignKey(o => o.CashierId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderDetail>(e =>
        {
            e.Property(d => d.UnitPrice).HasColumnType("decimal(18,0)");
            e.HasOne(d => d.Order)
             .WithMany(o => o.Details)
             .HasForeignKey(d => d.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Product)
             .WithMany(p => p.OrderDetails)
             .HasForeignKey(d => d.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
