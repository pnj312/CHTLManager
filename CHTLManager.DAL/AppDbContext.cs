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
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();
    public DbSet<PurchaseReceiptLine> PurchaseReceiptLines => Set<PurchaseReceiptLine>();

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

        modelBuilder.Entity<StockMovement>(e =>
        {
            e.Property(m => m.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.Reason).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.UnitPrice).HasColumnType("decimal(18,0)");
            e.HasOne(m => m.Product).WithMany().HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Order).WithMany().HasForeignKey(m => m.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.PurchaseReceipt).WithMany().HasForeignKey(m => m.PurchaseReceiptId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Supplier>(e => e.HasIndex(s => s.NameKey).IsUnique());

        modelBuilder.Entity<PurchaseReceipt>(e =>
        {
            e.HasIndex(r => r.Code).IsUnique();
            // Phiếu đã hủy không giữ chỗ số hóa đơn, để nhập lại được khi hủy nhầm.
            e.HasIndex(r => new { r.SupplierId, r.InvoiceKey }).IsUnique().HasFilter("\"Status\" <> 'Cancelled'");
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(r => r.Supplier).WithMany().HasForeignKey(r => r.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.CreatedBy).WithMany().HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.ConfirmedBy).WithMany().HasForeignKey(r => r.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseReceiptLine>(e =>
        {
            e.Property(l => l.UnitCost).HasColumnType("decimal(18,2)");
            e.HasOne(l => l.PurchaseReceipt).WithMany(r => r.Lines).HasForeignKey(l => l.PurchaseReceiptId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
