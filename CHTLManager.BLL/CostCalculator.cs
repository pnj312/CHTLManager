namespace CHTLManager.BLL;

/// <summary>Một lô trong bình quân: số hàng có giá đã có lúc lô vào, số đơn vị của lô, giá nhập.</summary>
public readonly record struct CostLot(int Before, int Quantity, decimal Cost);

/// <summary>Giá vốn bình quân gia quyền tức thời: tính lại sau mỗi lần nhập.</summary>
public static class CostCalculator
{
    /// <summary>Giá vốn mới sau khi nhập thêm <paramref name="quantity"/> đơn vị. Tồn âm được coi là 0.</summary>
    public static decimal AddToAverage(int stockBefore, decimal averageBefore, int quantity, decimal unitCost)
    {
        int onHand = Math.Max(stockBefore, 0);
        int total = onHand + quantity;
        if (quantity <= 0 || total <= 0) return averageBefore;
        // Còn hàng mà giá 0 nghĩa là chưa biết giá (dữ liệu từ trước khi có giá vốn), không phải hàng miễn phí.
        if (onHand > 0 && averageBefore <= 0) return Round(unitCost);
        return Round((onHand * averageBefore + quantity * unitCost) / total);
    }

    /// <summary>
    /// Đổi giá của <paramref name="quantity"/> đơn vị đã nằm trong bình quân từ giá cũ sang giá thật, số lượng tồn không đổi.
    /// Dùng cho lô chưa ghi: lúc đếm hàng vào kho theo giá vốn đang có, lúc xử lý mới biết giá nhập.
    /// Nếu đã bán bớt sau khi đếm, bình quân vẫn tính trên <paramref name="referenceStock"/> là số lúc đếm:
    /// hàng bán ra không làm đổi đơn giá, nên không được dồn cả lô vào số còn lại.
    /// </summary>
    public static decimal ReplaceInAverage(int stock, decimal average, int quantity, decimal oldCost, decimal newCost, int referenceStock = 0)
    {
        if (stock <= 0) return average;
        if (average <= 0) return Round(newCost);
        int basis = Math.Max(stock, referenceStock);
        return Round(Math.Max(0, (basis * average - quantity * oldCost + quantity * newCost) / basis));
    }

    /// <summary>
    /// Tính lại bình quân khi đổi giá hoặc bỏ một lô đã nằm trong đó: lùi qua các lô nhập sau để về đúng lúc trước lô này,
    /// đổi lô, rồi tính xuôi lại. Hàng bán ra không làm đổi đơn giá nên không cần tính đến.
    /// </summary>
    /// <param name="later">Các lô vào bình quân sau lô này, theo thứ tự.</param>
    /// <param name="newCost">Giá mới của lô; null nghĩa là bỏ lô.</param>
    public static decimal RecalculateLot(decimal average, CostLot lot, IReadOnlyList<CostLot> later, decimal? newCost)
    {
        var lots = later.Where(l => l.Quantity > 0).ToList();
        if (lot.Quantity <= 0) return average;
        // Một lô nhập khi kệ không còn hàng có giá đã thay hẳn bình quân, nên lô cũ không còn ảnh hưởng gì.
        if (lots.Any(l => l.Before <= 0)) return average;

        decimal current = average;
        for (int i = lots.Count - 1; i >= 0; i--)
            current = (current * (lots[i].Before + lots[i].Quantity) - lots[i].Quantity * lots[i].Cost) / lots[i].Before;

        // null: trước lô này không có hàng nào có giá.
        decimal? result = lot.Before > 0
            ? (current * (lot.Before + lot.Quantity) - lot.Quantity * lot.Cost) / lot.Before
            : null;
        if (newCost is decimal cost)
            result = result is decimal before ? (before * lot.Before + lot.Quantity * cost) / (lot.Before + lot.Quantity) : cost;

        int removed = newCost == null ? lot.Quantity : 0;
        foreach (var l in lots)
        {
            int stock = Math.Max(l.Before - removed, 0);
            result = result is decimal value && stock > 0 ? (value * stock + l.Quantity * l.Cost) / (stock + l.Quantity) : l.Cost;
        }

        return result is decimal final ? Round(Math.Max(0, final)) : average;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
