using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL.Interfaces;

public interface IProductService
{
    List<Product> GetAll();
    Product? GetById(int id);
    Product? GetByBarcode(string barcode);
    List<Product> Search(string keyword);
    Product Add(Product product);
    Product Update(Product product);

    /// <summary>Xóa sản phẩm. Trả về true nếu xóa hẳn, false nếu chỉ ngừng kinh doanh (do đã có trong hóa đơn).</summary>
    bool Delete(int id);

    /// <summary>Cộng (delta &gt; 0) hoặc trừ (delta &lt; 0) tồn kho.</summary>
    Product UpdateStock(int productId, int delta);

    /// <summary>Kiểm kê: đặt tồn kho bằng số lượng đếm thực tế (≥ 0). Trả về sản phẩm sau cập nhật.</summary>
    Product SetCountedStock(int productId, int countedStock);
}
