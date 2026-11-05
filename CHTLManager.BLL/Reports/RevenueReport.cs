using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL.Reports;

/// <summary>Báo cáo doanh thu trong một khoảng ngày.</summary>
public class RevenueReport
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }

    /// <summary>Danh sách hóa đơn trong khoảng (mới nhất trước), đã kèm thu ngân và chi tiết.</summary>
    public List<Order> Orders { get; init; } = new();

    public decimal TotalRevenue { get; init; }
    public int OrderCount { get; init; }
    public decimal AverageOrderValue { get; init; }

    /// <summary>Tổng số lượng sản phẩm đã bán.</summary>
    public int ItemsSold { get; init; }

    /// <summary>Doanh thu theo từng ngày (mới nhất trước).</summary>
    public List<DailyRevenue> Daily { get; init; } = new();
}

public class DailyRevenue
{
    public DateTime Date { get; init; }
    public int OrderCount { get; init; }
    public decimal Revenue { get; init; }
}
