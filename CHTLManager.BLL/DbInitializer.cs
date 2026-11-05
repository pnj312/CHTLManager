using CHTLManager.BLL.Security;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CHTLManager.BLL;

/// <summary>Tạo CSDL (nếu chưa có) và nạp dữ liệu mẫu cho lần chạy đầu tiên.</summary>
public static class DbInitializer
{
    public static void Initialize()
    {
        using var db = new AppDbContext();
        bool created = db.Database.EnsureCreated();
        if (!created) EnsureStockMovementsTable(db);

        if (!db.Users.Any())
        {
            db.Users.AddRange(
                new User { Username = "admin", PasswordHash = PasswordHasher.Hash("admin123"), FullName = "Quản trị viên", Role = UserRole.Admin },
                new User { Username = "thungan", PasswordHash = PasswordHasher.Hash("123456"), FullName = "Thu ngân 01", Role = UserRole.Cashier });
        }

        if (!db.Products.Any())
        {
            db.Products.AddRange(
                new Product { Barcode = "8934563138165", Name = "Mì Hảo Hảo tôm chua cay", Category = "Mì ăn liền", Price = 4500, Stock = 100 },
                new Product { Barcode = "8934563138172", Name = "Mì Omachi sườn hầm", Category = "Mì ăn liền", Price = 8000, Stock = 60 },
                new Product { Barcode = "8935049500018", Name = "Nước suối Aquafina 500ml", Category = "Đồ uống", Price = 6000, Stock = 120 },
                new Product { Barcode = "8934588012105", Name = "Coca-Cola lon 330ml", Category = "Đồ uống", Price = 10000, Stock = 80 },
                new Product { Barcode = "8935024140013", Name = "Trà xanh 0 độ 455ml", Category = "Đồ uống", Price = 10000, Stock = 70 },
                new Product { Barcode = "8934673101011", Name = "Sữa tươi Vinamilk 180ml", Category = "Sữa", Price = 8500, Stock = 90 },
                new Product { Barcode = "8934804023012", Name = "Bánh Oreo vani 133g", Category = "Bánh kẹo", Price = 16000, Stock = 40 },
                new Product { Barcode = "8935001710015", Name = "Snack Oishi tôm cay 40g", Category = "Bánh kẹo", Price = 7000, Stock = 55 },
                new Product { Barcode = "8936017360018", Name = "Cà phê G7 3in1 (hộp 18 gói)", Category = "Cà phê", Price = 52000, Stock = 25 },
                new Product { Barcode = "8934868150012", Name = "Dầu gội Clear 330g", Category = "Chăm sóc cá nhân", Price = 98000, Stock = 15 },
                new Product { Barcode = "8935210600019", Name = "Khăn giấy Pulppy 2 lớp", Category = "Gia dụng", Price = 12000, Stock = 50 },
                new Product { Barcode = "8936079120011", Name = "Bút bi Thiên Long TL-027", Category = "Văn phòng phẩm", Price = 4000, Stock = 200 });
        }

        db.SaveChanges();

        // Hóa đơn mẫu chỉ nạp cho CSDL vừa tạo mới, để màn hình doanh thu có số liệu ngay khi demo.
        if (created && !db.Orders.Any())
        {
            SeedSampleOrders(db, DateTime.Now);
            db.SaveChanges();
            SeedSampleStockMovements(db, DateTime.Now);
            db.SaveChanges();
        }
    }

    /// <summary>
    /// CSDL tạo từ phiên bản cũ chưa có bảng nhật ký kho, và EnsureCreated không thêm bảng vào CSDL đã có.
    /// shortcut: viết tay cho riêng bảng này; khi schema đổi tiếp thì chuyển sang EF Migrations.
    /// </summary>
    private static void EnsureStockMovementsTable(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "StockMovements" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_StockMovements" PRIMARY KEY AUTOINCREMENT,
                "ProductId" INTEGER NOT NULL,
                "UserId" INTEGER NOT NULL,
                "OrderId" INTEGER NULL,
                "CreatedAt" TEXT NOT NULL,
                "Type" TEXT NOT NULL,
                "BeforeStock" INTEGER NOT NULL,
                "AfterStock" INTEGER NOT NULL,
                "Delta" INTEGER NOT NULL,
                "Reason" TEXT NOT NULL,
                "UnitPrice" decimal(18,0) NOT NULL,
                CONSTRAINT "FK_StockMovements_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_StockMovements_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_StockMovements_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
            )
            """);
    }

    /// <summary>Vài dòng nhật ký kho mẫu (nhập hàng, thất thoát, đếm sai) để màn hình Nhật ký kho có số liệu khi demo.</summary>
    private static void SeedSampleStockMovements(AppDbContext db, DateTime now)
    {
        var admin = db.Users.FirstOrDefault(u => u.Role == UserRole.Admin);
        if (admin == null) return;

        void Add(string barcode, int daysAgo, int delta, StockMovementType type, StockReason reason)
        {
            var p = db.Products.FirstOrDefault(x => x.Barcode == barcode);
            if (p == null) return;

            int before = p.Stock;
            p.Stock = Math.Max(0, before + delta);
            db.StockMovements.Add(new StockMovement
            {
                ProductId = p.Id,
                UserId = admin.Id,
                CreatedAt = now.Date.AddDays(-daysAgo).AddHours(8),
                Type = type,
                BeforeStock = before,
                AfterStock = p.Stock,
                Delta = p.Stock - before,
                Reason = reason,
                UnitPrice = p.Price
            });
        }

        Add("8934588012105", 5, 24, StockMovementType.Import, StockReason.None);        // Coca: nhập thùng 24
        Add("8934804023012", 3, -2, StockMovementType.StockCount, StockReason.Lost);     // Oreo: mất 2
        Add("8934673101011", 2, -3, StockMovementType.StockCount, StockReason.Expired);  // Sữa: hết hạn 3
        Add("8935001710015", 1, -1, StockMovementType.StockCount, StockReason.Damaged);  // Snack: hỏng 1
    }

    /// <summary>
    /// Tạo hóa đơn bán hàng mẫu trong 7 ngày gần nhất (kết quả cố định nhờ seed ngẫu nhiên cố định),
    /// trừ tồn kho tương ứng để số liệu kho và doanh thu nhất quán.
    /// </summary>
    private static void SeedSampleOrders(AppDbContext db, DateTime now)
    {
        var rng = new Random(2026);
        var products = db.Products.Where(p => p.IsActive).OrderBy(p => p.Id).ToList();
        var cashier = db.Users.FirstOrDefault(u => u.Role == UserRole.Cashier);
        var admin = db.Users.FirstOrDefault(u => u.Role == UserRole.Admin);
        if (products.Count == 0 || cashier == null || admin == null) return;

        const int openMinute = 7 * 60, closeMinute = 21 * 60;

        for (int dayOffset = 6; dayOffset >= 0; dayOffset--)
        {
            DateTime day = now.Date.AddDays(-dayOffset);

            // Hôm nay chỉ tạo hóa đơn trước thời điểm hiện tại.
            int lastMinute = dayOffset == 0 ? Math.Min(closeMinute, (int)(now - now.Date).TotalMinutes - 5) : closeMinute;
            if (lastMinute <= openMinute) continue;

            int count = dayOffset == 0 ? 2 : rng.Next(3, 6);
            var times = Enumerable.Range(0, count)
                .Select(_ => day.AddMinutes(rng.Next(openMinute, lastMinute)))
                .OrderBy(t => t)
                .ToList();

            int seq = 0;
            foreach (var time in times)
            {
                var lines = new List<OrderDetail>();
                foreach (var p in products.OrderBy(_ => rng.Next()).Take(rng.Next(1, 5)))
                {
                    int qty = Math.Min(rng.Next(1, 4), p.Stock - 5);
                    if (qty <= 0) continue;

                    p.Stock -= qty;
                    lines.Add(new OrderDetail { ProductId = p.Id, Product = p, Quantity = qty, UnitPrice = p.Price });
                }
                if (lines.Count == 0) continue;

                decimal total = OrderService.CalculateTotal(lines);
                decimal step = total <= 50000 ? 10000 : 50000;
                decimal paid = Math.Ceiling(total / step) * step;

                seq++;
                db.Orders.Add(new Order
                {
                    Code = $"HD{day:yyyyMMdd}-{seq:D4}",
                    CreatedAt = time,
                    CashierId = rng.Next(4) == 0 ? admin.Id : cashier.Id,
                    Total = total,
                    CustomerPaid = paid,
                    Change = OrderService.CalculateChange(total, paid),
                    Details = lines
                });
            }
        }
    }
}
