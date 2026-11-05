namespace CHTLManager.BLL;

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

    /// <summary>Giá vốn sau khi bỏ một lô đã tính vào bình quân; hết hàng thì giữ nguyên để lần bán sau vẫn có số.</summary>
    public static decimal RemoveFromAverage(int stockBefore, decimal averageBefore, int quantity, decimal unitCost)
    {
        int remaining = stockBefore - quantity;
        if (remaining <= 0) return averageBefore;
        return Round(Math.Max(0, (stockBefore * averageBefore - quantity * unitCost) / remaining));
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
    /// Sửa giá một lô đã tính vào bình quân. <paramref name="pool"/> là số hàng lúc lô đó vào bình quân;
    /// <paramref name="dilution"/> là phần ảnh hưởng còn lại sau các lô nhập sau đó. Hàng đã bán không làm đổi đơn giá.
    /// </summary>
    public static decimal ChangeLotCost(decimal average, int pool, int quantity, decimal oldCost, decimal newCost, decimal dilution = 1)
    {
        if (pool <= 0) return average;
        return Round(Math.Max(0, average + quantity * (newCost - oldCost) / pool * dilution));
    }

    /// <summary>Bỏ một lô khỏi bình quân, tính trên số hàng lúc lô đó vào bình quân thay vì số còn lại hiện tại.</summary>
    public static decimal RemoveLot(decimal average, int pool, int quantity, decimal cost, decimal dilution = 1)
    {
        decimal without = RemoveFromAverage(pool, average, quantity, cost);
        return Round(Math.Max(0, average + (without - average) * dilution));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
