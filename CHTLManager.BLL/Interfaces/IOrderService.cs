using CHTLManager.BLL.Reports;
using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL.Interfaces;

public interface IOrderService
{
    /// <summary>Tạo hóa đơn: kiểm tra tồn kho, tính tiền, trừ kho và lưu lịch sử trong một giao dịch.</summary>
    Order CreateOrder(Order order, List<OrderDetail> details);

    List<Order> GetHistory(DateTime? from = null, DateTime? to = null);
    Order? GetById(int id);

    /// <summary>Báo cáo doanh thu từ ngày <paramref name="from"/> đến hết ngày <paramref name="to"/>.</summary>
    RevenueReport GetRevenueReport(DateTime from, DateTime to);
}
