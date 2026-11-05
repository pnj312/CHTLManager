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
    /// </summary>
    public static decimal ReplaceInAverage(int stock, decimal average, int quantity, decimal oldCost, decimal newCost)
    {
        if (stock <= 0) return average;
        return Round(Math.Max(0, (stock * average - quantity * oldCost + quantity * newCost) / stock));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
