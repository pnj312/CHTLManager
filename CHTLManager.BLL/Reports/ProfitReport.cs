namespace CHTLManager.BLL.Reports;

/// <summary>Lợi nhuận gộp trong một khoảng ngày. Chưa trừ chi phí vận hành như lương, mặt bằng.</summary>
public class ProfitReport
{
    public decimal Revenue { get; init; }
    public decimal CostOfGoods { get; init; }
    public decimal GrossProfit => Revenue - CostOfGoods;

    /// <summary>Số dòng hóa đơn có giá vốn tạm tính (bán ngoài sổ, chưa có giá nhập); càng nhiều thì lãi càng kém chắc chắn.</summary>
    public int EstimatedLineCount { get; init; }

    /// <summary>Lợi nhuận theo sản phẩm, lãi cao nhất trước.</summary>
    public List<ProductProfit> Products { get; init; } = new();
}

public class ProductProfit
{
    public string Name { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal Revenue { get; init; }
    public decimal Cost { get; init; }
    public decimal Profit => Revenue - Cost;
}
