using CHTLManager.BLL.Reports;
using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL.Interfaces;

public interface IProductService
{
    List<Product> GetAll();
    Product? GetById(int id);
    Product? GetByBarcode(string barcode);
    List<Product> Search(string keyword);
    /// <summary>Thêm sản phẩm; tồn kho ban đầu được ghi nhật ký dưới tên <paramref name="userId"/>.</summary>
    Product Add(Product product, int userId);
    Product Update(Product product);

    /// <summary>Xóa sản phẩm. Trả về true nếu xóa hẳn, false nếu chỉ ngừng kinh doanh (do đã có hóa đơn hoặc nhật ký kho).</summary>
    bool Delete(int id);

    /// <summary>
    /// Kiểm kê: đặt tồn kho bằng số đếm thực tế (≥ 0) và ghi nhật ký kho.
    /// Có chênh lệch thì bắt buộc có lý do đúng chiều; khớp thì lý do là None.
    /// <paramref name="invoiceNo"/> chỉ được lưu với lý do "Hàng nhập chưa ghi".
    /// </summary>
    Product SetCountedStock(int productId, int countedStock, StockReason reason, int userId, string? invoiceNo = null);

    /// <summary>Nhật ký kho từ ngày <paramref name="from"/> đến hết ngày <paramref name="to"/>, kèm tổng thất thoát.</summary>
    StockLogReport GetStockLog(DateTime from, DateTime to);
}
