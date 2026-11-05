namespace CHTLManager.BLL.Reports;

/// <summary>Lợi nhuận gộp trong một khoảng ngày. Chưa trừ chi phí vận hành như lương, mặt bằng.</summary>
public class ProfitReport
{
    public decimal Revenue { get; init; }
    public decimal CostOfGoods { get; init; }
    public decimal GrossProfit => Revenue - CostOfGoods;

    /// <summary>Hàng thiếu đã xử lý là thất thoát hoặc bồi thường, theo giá vốn.</summary>
    public decimal LossExpense { get; init; }

    /// <summary>Hàng thừa không rõ nguồn, theo giá vốn.</summary>
    public decimal OtherIncome { get; init; }

    public decimal CompensationCollected { get; init; }

    /// <summary>Không vào lãi nhưng hiện riêng, để "đếm sai" không che được thất thoát.</summary>
    public decimal MiscountShortValue { get; init; }
    public decimal MiscountSurplusValue { get; init; }

    public decimal NetAfterStockDifferences => GrossProfit - LossExpense + OtherIncome + CompensationCollected;

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
