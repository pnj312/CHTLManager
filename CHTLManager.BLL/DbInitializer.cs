using CHTLManager.BLL.Security;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL;

/// <summary>Tạo CSDL (nếu chưa có) và nạp dữ liệu mẫu cho lần chạy đầu tiên.</summary>
public static class DbInitializer
{
    public static void Initialize()
    {
        using var db = new AppDbContext();
        db.Database.EnsureCreated();

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
    }
}
