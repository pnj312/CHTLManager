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
        if (product.AverageCost < 0) throw new BusinessException("Giá vốn không được âm.");

        using var db = _factory();
        string code = product.Barcode.Trim();
        var existing = db.Products.FirstOrDefault(p => p.Barcode == code);
        if (existing != null)
        {
            if (existing.IsActive)
                throw new BusinessException($"Mã vạch '{code}' đã tồn tại.");

            // Mã vạch thuộc sản phẩm đã ngừng kinh doanh: kinh doanh lại sản phẩm đó với thông tin mới
            // (giữ nguyên Id để lịch sử hóa đơn cũ vẫn trỏ đúng sản phẩm). Tồn kho không đổi:
            // cho khai lại ở đây sẽ thành cửa sau né kiểm kê, nhập kho và giấu thất thoát.
            existing.Name = product.Name.Trim();
            existing.Category = (product.Category ?? string.Empty).Trim();
            existing.Price = product.Price;
            existing.IsActive = true;
            db.SaveChanges();
            return existing;
        }

        var entity = new Product
        {
            Barcode = code,
            Name = product.Name.Trim(),
            Category = (product.Category ?? string.Empty).Trim(),
            Price = product.Price,
            AverageCost = product.AverageCost,
            LastCost = product.AverageCost,
            IsActive = true
        };
        db.Products.Add(entity);
        StockLedger.Apply(db, entity, product.Stock, StockMovementType.Initial, userId);
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
        // Không sửa tồn kho ở đây (tránh ghi đè số đã bán); tồn kho chỉ thay đổi qua phiếu nhập, kiểm kê hoặc bán hàng.
        db.SaveChanges();
        return entity;
    }

    public bool Delete(int id)
    {
        using var db = _factory();
        var entity = db.Products.FirstOrDefault(p => p.Id == id)
                     ?? throw new BusinessException("Không tìm thấy sản phẩm cần xóa.");

        if (entity.Stock != 0)
            throw new BusinessException($"'{entity.Name}' còn tồn kho {entity.StockDisplay}. Hãy bán hết hoặc kiểm kê về 0 trước khi ngừng kinh doanh.");

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

    public void SetOpeningCost(int productId, decimal cost, int adminId)
    {
        if (cost <= 0)
            throw new BusinessException("Giá vốn phải lớn hơn 0.");

        using var db = _factory();
        var entity = db.Products.FirstOrDefault(p => p.Id == productId && p.IsActive)
                     ?? throw new BusinessException("Không tìm thấy sản phẩm.");

        // Chỉ khai báo khi chưa có: sửa giá vốn đang dùng sẽ làm sai lợi nhuận mà không có chứng từ nào giải thích.
        if (entity.AverageCost > 0)
            throw new BusinessException($"'{entity.Name}' đã có giá vốn {entity.AverageCost:N0} đ. Giá vốn thay đổi qua phiếu nhập.");

        entity.AverageCost = cost;
        if (entity.LastCost <= 0) entity.LastCost = cost;
        db.StockMovements.Add(new StockMovement
        {
            ProductId = entity.Id,
            UserId = adminId,
            CreatedAt = DateTime.Now,
            Type = StockMovementType.CostDeclared,
            BeforeStock = entity.Stock,
            AfterStock = entity.Stock,
            UnitPrice = entity.Price,
            UnitCost = cost
        });
        db.SaveChanges();
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
            OutOfBookQuantity = movements.Sum(m => m.OutOfBookQuantity)
        };
    }

    private static void Validate(Product p)
    {
        if (p == null) throw new BusinessException("Dữ liệu sản phẩm không hợp lệ.");
        if (string.IsNullOrWhiteSpace(p.Barcode)) throw new BusinessException("Mã vạch không được để trống.");
        if (string.IsNullOrWhiteSpace(p.Name)) throw new BusinessException("Tên sản phẩm không được để trống.");
        if (p.Price <= 0) throw new BusinessException("Giá bán phải lớn hơn 0.");
    }
}
