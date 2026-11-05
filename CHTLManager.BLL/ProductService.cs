using CHTLManager.BLL.Interfaces;
using CHTLManager.BLL.Reports;
using CHTLManager.BLL.Security;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CHTLManager.BLL;

/// <summary>Quản lý sản phẩm: xem, thêm, sửa, xóa, tìm theo mã vạch/tên, cập nhật tồn kho.</summary>
public class ProductService : IProductService
{
    private readonly Func<AppDbContext> _factory;

    public ProductService() : this(() => new AppDbContext())
    {
    }

    public ProductService(Func<AppDbContext> factory)
    {
        _factory = factory;
    }

    public List<Product> GetAll()
    {
        using var db = _factory();
        return db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToList();
    }

    public Product? GetById(int id)
    {
        using var db = _factory();
        return db.Products.AsNoTracking().FirstOrDefault(p => p.Id == id && p.IsActive);
    }

    public Product? GetByBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;
        string code = barcode.Trim();

        using var db = _factory();
        return db.Products.AsNoTracking().FirstOrDefault(p => p.Barcode == code && p.IsActive);
    }

    public List<Product> Search(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return GetAll();
        string key = TextHelper.RemoveDiacritics(keyword.Trim());

        // Danh mục của một cửa hàng tiện lợi nhỏ nên lọc trong bộ nhớ để hỗ trợ tìm không phân biệt dấu/hoa thường
        // (SQLite chỉ so sánh không phân biệt hoa thường với ký tự ASCII).
        return GetAll()
            .Where(p => TextHelper.RemoveDiacritics(p.Name).Contains(key) || p.Barcode.Contains(key))
            .ToList();
    }

    public Product Add(Product product, int userId)
    {
        Validate(product);
        if (product.Stock < 0) throw new BusinessException("Số lượng tồn không được âm.");

        using var db = _factory();
        string code = product.Barcode.Trim();
        var existing = db.Products.FirstOrDefault(p => p.Barcode == code);
        if (existing != null)
        {
            if (existing.IsActive)
                throw new BusinessException($"Mã vạch '{code}' đã tồn tại.");

            // Mã vạch thuộc sản phẩm đã ngừng kinh doanh: kinh doanh lại sản phẩm đó với thông tin mới
            // (giữ nguyên Id để lịch sử hóa đơn cũ vẫn trỏ đúng sản phẩm).
            existing.Name = product.Name.Trim();
            existing.Category = (product.Category ?? string.Empty).Trim();
            existing.Price = product.Price;
            existing.IsActive = true;
            ChangeStock(db, existing, product.Stock, StockMovementType.Initial, StockReason.None, userId);
            db.SaveChanges();
            return existing;
        }

        var entity = new Product
        {
            Barcode = code,
            Name = product.Name.Trim(),
            Category = (product.Category ?? string.Empty).Trim(),
            Price = product.Price,
            IsActive = true
        };
        db.Products.Add(entity);
        ChangeStock(db, entity, product.Stock, StockMovementType.Initial, StockReason.None, userId);
        db.SaveChanges();
        return entity;
    }

    public Product Update(Product product)
    {
        Validate(product);

        using var db = _factory();
        var entity = db.Products.FirstOrDefault(p => p.Id == product.Id)
                     ?? throw new BusinessException("Không tìm thấy sản phẩm cần sửa.");

        string code = product.Barcode.Trim();
        var conflict = db.Products.FirstOrDefault(p => p.Barcode == code && p.Id != product.Id);
        if (conflict != null)
            throw new BusinessException(conflict.IsActive
                ? $"Mã vạch '{code}' đã được dùng cho sản phẩm khác."
                : $"Mã vạch '{code}' thuộc sản phẩm đã ngừng kinh doanh '{conflict.Name}'. Hãy dùng mã khác.");

        entity.Barcode = code;
        entity.Name = product.Name.Trim();
        entity.Category = (product.Category ?? string.Empty).Trim();
        entity.Price = product.Price;
        // Không sửa tồn kho ở đây (tránh ghi đè số đã bán); tồn kho chỉ thay đổi qua UpdateStock hoặc bán hàng.
        db.SaveChanges();
        return entity;
    }

    public bool Delete(int id)
    {
        using var db = _factory();
        var entity = db.Products.FirstOrDefault(p => p.Id == id)
                     ?? throw new BusinessException("Không tìm thấy sản phẩm cần xóa.");

        bool hasHistory = db.OrderDetails.Any(d => d.ProductId == id) || db.StockMovements.Any(m => m.ProductId == id);
        if (hasHistory)
        {
            // Giữ nguyên lịch sử bán hàng và nhật ký kho: chỉ ngừng kinh doanh (xóa mềm).
            entity.IsActive = false;
            db.SaveChanges();
            return false;
        }

        db.Products.Remove(entity);
        db.SaveChanges();
        return true;
    }

    public Product ImportStock(int productId, int quantity, string supplier, string invoiceNo, int userId)
    {
        if (quantity <= 0)
            throw new BusinessException("Số lượng nhập phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(supplier))
            throw new BusinessException("Hãy nhập tên nhà cung cấp.");
        if (string.IsNullOrWhiteSpace(invoiceNo))
            throw new BusinessException("Hãy nhập số hóa đơn của nhà cung cấp.");

        using var db = _factory();
        var entity = db.Products.FirstOrDefault(p => p.Id == productId && p.IsActive)
                     ?? throw new BusinessException("Không tìm thấy sản phẩm.");

        // Hàng bán ngoài sổ thuộc lô trước: cộng thẳng vào lô mới sẽ làm lẫn hai lô và sai số tồn.
        if (entity.Stock < 0)
            throw new BusinessException($"'{entity.Name}' đã bán {-entity.Stock} cái ngoài sổ từ lô trước. Hãy kiểm kê trước khi nhập lô mới.");

        ChangeStock(db, entity, entity.Stock + quantity, StockMovementType.Import, StockReason.None, userId,
            supplier: supplier.Trim(), invoiceNo: invoiceNo.Trim());
        db.SaveChanges();
        return entity;
    }

    public Product SetCountedStock(int productId, int countedStock, StockReason reason, int userId, string? invoiceNo = null)
    {
        if (countedStock < 0)
            throw new BusinessException("Số lượng kiểm kê phải là số nguyên không âm.");

        using var db = _factory();
        var entity = db.Products.FirstOrDefault(p => p.Id == productId && p.IsActive)
                     ?? throw new BusinessException("Không tìm thấy sản phẩm.");

        int diff = countedStock - entity.Stock;
        bool reasonFits = diff switch
        {
            < 0 => reason is StockReason.Lost or StockReason.Damaged or StockReason.Expired or StockReason.MiscountBefore,
            > 0 => reason is StockReason.UnrecordedImport or StockReason.MiscountBefore,
            _ => reason == StockReason.None
        };
        if (!reasonFits)
            throw new BusinessException("Hãy chọn lý do chênh lệch phù hợp.");

        // Số hóa đơn chỉ có nghĩa khi kiểm kê phát hiện lô hàng chưa ghi.
        string? invoice = reason == StockReason.UnrecordedImport && !string.IsNullOrWhiteSpace(invoiceNo) ? invoiceNo.Trim() : null;
        ChangeStock(db, entity, countedStock, StockMovementType.StockCount, reason, userId, logUnchanged: true, invoiceNo: invoice);
        db.SaveChanges();
        return entity;
    }

    public StockLogReport GetStockLog(DateTime from, DateTime to)
    {
        DateTime start = from.Date, end = to.Date;
        if (start > end)
            throw new BusinessException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        DateTime endExclusive = end.AddDays(1);

        using var db = _factory();
        var movements = db.StockMovements
            .AsNoTracking()
            .Include(m => m.Product)
            .Include(m => m.User)
            .Include(m => m.Order)
            .Where(m => m.CreatedAt >= start && m.CreatedAt < endExclusive)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .ToList();

        return new StockLogReport
        {
            Movements = movements,
            LossValue = movements.Sum(m => m.LossValue),
            LossQuantity = movements.Where(m => m.IsLoss).Sum(m => -m.Delta),
            OutOfBookQuantity = movements.Sum(m => m.OutOfBookQuantity)
        };
    }

    /// <summary>Đổi tồn kho và ghi một dòng nhật ký; người gọi lưu cả hai trong cùng một SaveChanges.</summary>
    private static void ChangeStock(AppDbContext db, Product product, int newStock, StockMovementType type,
        StockReason reason, int userId, bool logUnchanged = false, string? supplier = null, string? invoiceNo = null)
    {
        int before = product.Stock;
        product.Stock = newStock;
        if (before == newStock && !logUnchanged) return;

        db.StockMovements.Add(new StockMovement
        {
            Product = product,
            UserId = userId,
            CreatedAt = DateTime.Now,
            Type = type,
            BeforeStock = before,
            AfterStock = newStock,
            Delta = newStock - before,
            Reason = reason,
            UnitPrice = product.Price,
            Supplier = supplier,
            InvoiceNo = invoiceNo
        });
    }

    private static void Validate(Product p)
    {
        if (p == null) throw new BusinessException("Dữ liệu sản phẩm không hợp lệ.");
        if (string.IsNullOrWhiteSpace(p.Barcode)) throw new BusinessException("Mã vạch không được để trống.");
        if (string.IsNullOrWhiteSpace(p.Name)) throw new BusinessException("Tên sản phẩm không được để trống.");
        if (p.Price <= 0) throw new BusinessException("Giá bán phải lớn hơn 0.");
    }
}
