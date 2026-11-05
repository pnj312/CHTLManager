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

    /// <summary>Hủy cả hóa đơn: hoàn kho theo giá vốn đã lưu, ghi nhật ký; hóa đơn vẫn còn để đối chiếu.</summary>
    void CancelOrder(int orderId, string reason, int adminId);

    List<Order> GetHistory(DateTime? from = null, DateTime? to = null);
    Order? GetById(int id);

    /// <summary>Báo cáo doanh thu từ ngày <paramref name="from"/> đến hết ngày <paramref name="to"/>.</summary>
    RevenueReport GetRevenueReport(DateTime from, DateTime to);

    /// <summary>Lãi gộp (doanh thu trừ giá vốn) từ ngày <paramref name="from"/> đến hết ngày <paramref name="to"/>.</summary>
    ProfitReport GetProfitReport(DateTime from, DateTime to);
}
