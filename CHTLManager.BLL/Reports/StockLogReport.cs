using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL.Reports;

/// <summary>Nhật ký kho trong một khoảng ngày.</summary>
public class StockLogReport
{
    public List<StockMovement> Movements { get; init; } = new();

    public int OutOfBookQuantity { get; init; }
}
