using CHTLManager.BLL.Reports;
using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL.Interfaces;

public interface IOrderService
{
    /// <summary>
    /// Tạo hóa đơn: kiểm tra tồn kho, tính tiền, trừ kho và lưu lịch sử trong một giao dịch.
    /// Sản phẩm có Id trong <paramref name="allowOversellProductIds"/> được bán vượt tồn kho
    /// (thu ngân đã xác nhận hàng thực tế vẫn còn); tồn kho của chúng có thể âm.
    /// </summary>
    Order CreateOrder(Order order, List<OrderDetail> details, ISet<int>? allowOversellProductIds = null);

    List<Order> GetHistory(DateTime? from = null, DateTime? to = null);
    Order? GetById(int id);

    /// <summary>Báo cáo doanh thu từ ngày <paramref name="from"/> đến hết ngày <paramref name="to"/>.</summary>
    RevenueReport GetRevenueReport(DateTime from, DateTime to);
}
