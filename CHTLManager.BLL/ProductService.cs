using CHTLManager.BLL.Interfaces;
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

    public Product Add(Product product)
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
            existing.Stock = product.Stock;
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
            Stock = product.Stock,
            IsActive = true
        };
        db.Products.Add(entity);
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

        bool usedInOrders = db.OrderDetails.Any(d => d.ProductId == id);
        if (usedInOrders)
        {
            // Giữ nguyên lịch sử bán hàng: chỉ ngừng kinh doanh (xóa mềm).
            entity.IsActive = false;
            db.SaveChanges();
            return false;
        }

        db.Products.Remove(entity);
        db.SaveChanges();
        return true;
    }

    public Product UpdateStock(int productId, int delta)
    {
        using var db = _factory();
        var entity = db.Products.FirstOrDefault(p => p.Id == productId && p.IsActive)
                     ?? throw new BusinessException("Không tìm thấy sản phẩm.");

        int newStock = entity.Stock + delta;
        if (newStock < 0)
            throw new BusinessException($"Tồn kho không đủ (hiện còn {entity.Stock}).");

        entity.Stock = newStock;
        db.SaveChanges();
        return entity;
    }

    public Product SetCountedStock(int productId, int countedStock)
    {
        if (countedStock < 0)
            throw new BusinessException("Số lượng kiểm kê phải là số nguyên không âm.");

        using var db = _factory();
        var entity = db.Products.FirstOrDefault(p => p.Id == productId && p.IsActive)
                     ?? throw new BusinessException("Không tìm thấy sản phẩm.");

        entity.Stock = countedStock;
        db.SaveChanges();
        return entity;
    }

    private static void Validate(Product p)
    {
        if (p == null) throw new BusinessException("Dữ liệu sản phẩm không hợp lệ.");
        if (string.IsNullOrWhiteSpace(p.Barcode)) throw new BusinessException("Mã vạch không được để trống.");
        if (string.IsNullOrWhiteSpace(p.Name)) throw new BusinessException("Tên sản phẩm không được để trống.");
        if (p.Price <= 0) throw new BusinessException("Giá bán phải lớn hơn 0.");
    }
}
