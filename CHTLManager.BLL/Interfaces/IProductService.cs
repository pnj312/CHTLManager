using CHTLManager.BLL.Reports;
using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL.Interfaces;

public interface IProductService
{
    List<Product> GetAll();
    Product? GetById(int id);
    Product? GetByBarcode(string barcode);
    List<Product> Search(string keyword);
    /// <summary>Thêm sản phẩm, <c>AverageCost</c> là giá vốn ban đầu; tồn kho ban đầu được ghi nhật ký dưới tên <paramref name="userId"/>.</summary>
    Product Add(Product product, int userId);
    Product Update(Product product);

    /// <summary>Xóa sản phẩm. Trả về true nếu xóa hẳn, false nếu chỉ ngừng kinh doanh (do đã có hóa đơn hoặc nhật ký kho).</summary>
    bool Delete(int id);

    /// <summary>Khai báo giá vốn cho sản phẩm chưa có giá (thường là hàng tồn từ trước khi có giá vốn). Có ghi nhật ký.</summary>
    void SetOpeningCost(int productId, decimal cost, int adminId);

    /// <summary>Nhật ký kho từ ngày <paramref name="from"/> đến hết ngày <paramref name="to"/>.</summary>
    StockLogReport GetStockLog(DateTime from, DateTime to);
}
