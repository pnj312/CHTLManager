using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL.Reports;

/// <summary>Nhật ký kho trong một khoảng ngày.</summary>
public class StockLogReport
{
    public List<StockMovement> Movements { get; init; } = new();

    /// <summary>Tổng giá trị thất thoát (mất, hỏng, hết hạn) tính theo giá bán lúc kiểm kê.</summary>
    public decimal LossValue { get; init; }
    public int LossQuantity { get; init; }
    public int OutOfBookQuantity { get; init; }
}
